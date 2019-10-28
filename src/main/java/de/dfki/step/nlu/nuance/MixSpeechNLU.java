package de.dfki.step.nlu.nuance;

import com.google.gson.FieldNamingPolicy;
import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import de.dfki.step.core.Token;

import javax.websocket.*;
import java.io.IOException;
import java.io.PipedInputStream;
import java.io.PipedOutputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.URI;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

import de.dfki.step.core.TokenComponent;
import de.dfki.step.nlu.nuance.json.*;
import de.dfki.step.nlu.nuance.json.response.ASR_NLU_Response;
import de.dfki.step.nlu.nuance.json.response.Entity;
import de.dfki.step.nlu.nuance.json.response.Interpretation;
import de.dfki.step.nlu.nuance.json.response.Rec_Text_Result;
import de.dfki.step.nlu.nuance.json.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Creates a connection to Nuance Mix using the Credentials in MixCredentials.
 * After starting the service, a TCP port is opened at the specific IP which allows
 * to transmit audio (OPUS, 16khz, MONO) to Nuance Mix.
 * Recording is started after calling start and finished after calling finished.
 * The result is then provided
 */
@ClientEndpoint
public class MixSpeechNLU {

    private static Logger log = LoggerFactory.getLogger(MixSpeechNLU.class);

    static boolean Debug = true;

    enum MixConnectionStatus
    {
        NOT_CONNECTED,
        CONNECTING,
        CONNECTED,
        SENDING_QUERY,
        SENDING_AUDIO,
        STOP_SENDING_AUDIO,
        WAIT_FOR_REC_TEXT_RESULTS,
        WAIT_FOR_NLU_INTERPRETATION_RESULTS,
        WAIT_FOR_QUERY_END
    }

    String serverIP;
    int serverPort;
    ServerSocket serverSocket;
    Thread serverThread;

    boolean recordingVoice = false;
    boolean serviceRunning = false;

    PipedOutputStream pipedOutputStream;
    PipedInputStream pipedInputStream;

    WebSocketContainer mix;
    Session mixSession;
    MixConnectionStatus mixStatus = MixConnectionStatus.NOT_CONNECTED;
    String mixSessionID;
    String mixUserID;
    String mixDeviceID;
    int mixTransactionID;
    int mixAudioID;

    Thread NuanceAudioSendThread;

    Gson gson = new GsonBuilder().setFieldNamingPolicy(FieldNamingPolicy.LOWER_CASE_WITH_UNDERSCORES).disableHtmlEscaping().create();

    TokenComponent token;

    /**
     * Create a Nuance Mix Service
     * @param audioServerIP IP to bind for the TCP port which recieves the audio data
     * @param audioServerPort port to bind for the TCP port which recieves the audio data
     */
    public MixSpeechNLU(String audioServerIP, int audioServerPort, TokenComponent token) throws IOException {
        this.serverIP = audioServerIP;
        this.serverPort = audioServerPort;
        this.token = token;

        this.pipedOutputStream = new PipedOutputStream();
        this.pipedInputStream = new PipedInputStream(this.pipedOutputStream);
    }

    class TCPServer implements Runnable {
        MixSpeechNLU parent;

        public TCPServer(MixSpeechNLU p) {
            this.parent = p;
        }

        public void run() {
            while(this.parent.serviceRunning) {
                try {
                    // let one client connect
                    log.info("[MIX] waiting for audio client to connect...");
                    Socket client = this.parent.serverSocket.accept();
                    log.info("[MIX] Audio Client connected: " + client.getInetAddress().getHostAddress());

                    ByteBuffer AudioCache = ByteBuffer.allocate(1024 * 30);
                    int AudioCachePos = 0;
                    byte[] buffer = new byte[42];
                    int length = -1;

                    while(this.parent.serviceRunning && !client.isClosed()) {
                        length = client.getInputStream().read(buffer, 0, buffer.length);

                        // write voice directly to Nuance Mix
                        if(length > 0 && this.parent.recordingVoice && this.parent.mixStatus == MixConnectionStatus.SENDING_AUDIO) {
                            if(AudioCachePos > 0) {
                                log.info("[MIX] sending " + AudioCachePos + " remaining bytes");
                                AudioCache.limit(AudioCachePos);
                               // this.parent.mixSession.getBasicRemote().sendBinary(AudioCache);
                                AudioCachePos = 0;
                                AudioCache.position(0);
                            }

                            this.parent.mixSession.getBasicRemote().sendBinary(ByteBuffer.wrap(buffer, 0, length));
                        }
                        // Buffer Voice and send it then to Nuance Mix
                        else if(length > 0 && this.parent.recordingVoice) {
                            AudioCache.put(buffer, 0, length);
                            AudioCachePos += length;
                        }

                        // End of transmission of Voice
                        if(this.parent.mixStatus == MixConnectionStatus.STOP_SENDING_AUDIO)
                        {
                            // Reset Cache
                            AudioCachePos = 0;
                            AudioCache.position(0);
                            AudioCache.limit(1024 * 30);

                            // send Audio end Frame
                            AudioEnd audioEnd = new AudioEnd();
                            audioEnd.audio_id = this.parent.mixAudioID;

                            String jsonQueryEnd = gson.toJson(audioEnd);
                            try {
                                this.parent.mixSession.getBasicRemote().sendText(jsonQueryEnd);
                            } catch (IOException e) {
                                e.printStackTrace();
                            }

                            this.parent.mixStatus = MixConnectionStatus.WAIT_FOR_REC_TEXT_RESULTS;

                            if(Debug)
                            {
                                log.info("[MIX-DEBUG] send package to Nuance Mix");
                                log.info(jsonQueryEnd);
                            }
                        }
                    }

                    if(1 == 1)
                        return;

                    // read data from socket
                    while(this.parent.serviceRunning && !client.isClosed()) {
                        length = client.getInputStream().read(buffer, 0, buffer.length);

                        // write voice data into pipe as long as necessary
                        if(length > 0 && this.parent.recordingVoice)
                            this.parent.pipedOutputStream.write(buffer,0, length);
                    }
                } catch (IOException e) {
                    e.printStackTrace();
                }
            }
        }
    }


    class AudioSender implements Runnable {
        MixSpeechNLU parent;

        public AudioSender(MixSpeechNLU p) {
            this.parent = p;
        }

        public void run() {
            log.info("[MIX] audio pipe to Nuance Mix thread started");
            int totalBytes = 0;

            if(1 == 1)
                return;

            try {
                byte[] buffer = new byte[41];
                int length = -1;

                while (this.parent.mixStatus == MixConnectionStatus.SENDING_AUDIO && this.parent.recordingVoice) {
                    if(this.parent.pipedInputStream.available() <= 0)
                    {
                        Thread.sleep(10);
                        continue;
                    }
                    length = this.parent.pipedInputStream.read(buffer, 0, buffer.length);
                    totalBytes += length;

                    if (length > 0) {
                        this.parent.mixSession.getAsyncRemote().sendBinary(ByteBuffer.wrap(buffer, 0, length));
                    }
                }

                // send last bit of data
                while (this.parent.pipedInputStream.available() > 0) {
                    length = this.parent.pipedInputStream.read(buffer, 0, buffer.length);
                    totalBytes += length;

                    if (length > 0) {
                        this.parent.mixSession.getAsyncRemote().sendBinary(ByteBuffer.wrap(buffer, 0, length));
                    }
                }

                // finish, all data send
                log.info("[MIX] audio pipe to Nuance Mix thread terminated successful (" + totalBytes + " Bytes)");
            }
            catch(Exception ex)
            {
                log.info("[MIX] audio pipe to Nuance Mix thread crashed! (" + totalBytes + " Bytes). msg:" + ex.getMessage());
            }
            finally
            {
                AudioEnd audioEnd = new AudioEnd();
                audioEnd.audio_id = this.parent.mixAudioID;

                String jsonQueryEnd = gson.toJson(audioEnd);
                try {
                    this.parent.mixSession.getBasicRemote().sendText(jsonQueryEnd);
                } catch (IOException e) {
                    e.printStackTrace();
                }

                this.parent.mixStatus = MixConnectionStatus.WAIT_FOR_REC_TEXT_RESULTS;

                if(Debug)
                {
                    log.info("[MIX-DEBUG] send package to Nuance Mix");
                    log.info(jsonQueryEnd);
                }
            }
        }
    }

    /**
     * Starts the Nuance Mix service and creates the TCP server to deliver audio
     */
    public void StartService() throws IOException {
        if(serviceRunning)
            return;

        // set service running in order to let "endless" loops of the TCP server run
        this.serviceRunning = true;

        // Starting server thread for TCP connection delivering audio data
        this.serverSocket = new ServerSocket(this.serverPort, 1, InetAddress.getByName(this.serverIP));
        this.serverThread = new Thread(new TCPServer(this));
        this.serverThread.start();
    }

    /**
     * Stops the Nuance Mix service and stops the TCP server
     */
    public void StopService()
    {
        // stop loops of the TCP server
        this.serviceRunning = false;

        this.mixStatus = MixConnectionStatus.NOT_CONNECTED;
    }

    /**
     * Starts the recording of the voice and sends it over to Nuance Mix
     */
    public void StartRecording() throws IOException, DeploymentException {

        // empty pipe in case there is still something in
        this.pipedInputStream.skip(this.pipedInputStream.available());

        // start recording
        this.recordingVoice = true;

        // connect to Nuance Mix service
        this.mix = ContainerProvider.getWebSocketContainer();
        this.mix.connectToServer(this, URI.create(MixCredentials.SERVER_URI));
        this.mixStatus = MixConnectionStatus.CONNECTING;

        // send connect package
        Connect connectJSON = new Connect();

        connectJSON.app_id = MixCredentials.APP_ID;
        connectJSON.app_key = MixCredentials.APP_KEY;
        connectJSON.codec = "audio/opus;rate=16000";
        connectJSON.context_tag = MixCredentials.CONTEXT_TAG;
        this.mixUserID = java.util.UUID.randomUUID().toString();
        connectJSON.user_id = this.mixUserID;
        this.mixDeviceID = java.util.UUID.randomUUID().toString();
        connectJSON.device_id = this.mixDeviceID;

        String jsonRequest = this.gson.toJson(connectJSON);

        if(Debug)
        {
            log.info("[MIX-DEBUG] send connect package to Nuance Mix");
            log.info(jsonRequest);
        }

        this.mixSession.getBasicRemote().sendText(jsonRequest);
    }

    /**
     * Ends the recording session and Nuance Mix will analyze the result
     */
    public void StopRecording()
    {
        // stop recording and transmitting
        this.recordingVoice = false;
        this.mixStatus = MixConnectionStatus.STOP_SENDING_AUDIO;

        // wait for thread to terminate
        try {
            this.NuanceAudioSendThread.join();
        } catch (InterruptedException e) {
            e.printStackTrace();
        }
    }

    public boolean isRecording()
    {
        return this.recordingVoice;
    }

    private void HandleIntent(MixIntent intent)
    {
        if(Debug)
        {
            log.info("[MIX-DEBUG] intent detected: " + intent.intent);
        }

        Token intentToken = new Token().add("intent", intent.intent).add("confidence", intent.confidence);
        this.token.addToken(intentToken);
    }


    /**
     * Gets called when Nuance Mix sends a Websocket connection back
     * @param message content of the WebSocket message
     */
    @OnMessage
    public void onMessage(String message) throws IOException {
        if(Debug)
        {
            log.info("[MIX-DEBUG] received package from Nuance Mix");
            log.info(message);
        }

        switch(this.mixStatus)
        {
            case CONNECTING:
                Message response = gson.fromJson(message, Message.class);

                if(!response.message.equalsIgnoreCase("connected"))
                {
                    // something went terrible wrong!
                    log.info("[MIX] connection to Nuance Mix failed (auth error?)");
                    return;
                }

                ConnectResponse connectResponse = gson.fromJson(message, ConnectResponse.class);
                mixSessionID = connectResponse.session_id;


                // now we are connected to Nuance Mix => next step is to request Speech NLU service
                this.mixStatus = MixConnectionStatus.CONNECTED;

                // Start Query
                this.mixStatus = MixConnectionStatus.SENDING_QUERY;

                QueryBegin querySpeechNLU = new QueryBegin();
                querySpeechNLU.command = "NDSP_ASR_APP_CMD";
                querySpeechNLU.context_tag = MixCredentials.CONTEXT_TAG;
                querySpeechNLU.language = MixCredentials.LANGUAGE;
                this.mixTransactionID = 1;
                querySpeechNLU.transaction_id = this.mixTransactionID;

                String jsonRequest = gson.toJson(querySpeechNLU);
                this.mixSession.getBasicRemote().sendText(jsonRequest);

                if(Debug)
                {
                    log.info("[MIX-DEBUG] send package to Nuance Mix");
                    log.info(jsonRequest);
                }

                // Send Query Parameter
                QueryParameter queryParameter = new QueryParameter();
                this.mixAudioID = 1;
                queryParameter.audio_id = this.mixAudioID;
                queryParameter.parameter_name = "AUDIO_INFO";
                queryParameter.parameter_type = "audio";
                queryParameter.transaction_id = this.mixTransactionID;

                String jsonQueryParameter = gson.toJson(queryParameter);
                this.mixSession.getBasicRemote().sendText(jsonQueryParameter);

                if(Debug)
                {
                    log.info("[MIX-DEBUG] send package to Nuance Mix");
                    log.info(jsonQueryParameter);
                }

                // send Query End Command
                QueryEnd queryEnd = new QueryEnd();
                queryEnd.transaction_id = this.mixTransactionID;

                String jsonQueryEnd = gson.toJson(queryEnd);
                this.mixSession.getBasicRemote().sendText(jsonQueryEnd);

                if(Debug)
                {
                    log.info("[MIX-DEBUG] send package to Nuance Mix");
                    log.info(jsonQueryEnd);
                }

                // Send Begin of Audio
                Audio audio = new Audio();
                audio.audio_id = this.mixAudioID;

                String jsonAudioBegin = gson.toJson(audio);
                try {
                    this.mixSession.getBasicRemote().sendText(jsonAudioBegin);
                } catch (IOException e) {
                    e.printStackTrace();
                }

                if(Debug)
                {
                    log.info("[MIX-DEBUG] send package to Nuance Mix");
                    log.info(jsonAudioBegin);
                }

                this.mixStatus = MixConnectionStatus.SENDING_AUDIO;

                this.NuanceAudioSendThread = new Thread(new AudioSender(this));
                this.NuanceAudioSendThread.start();

                break;

            case WAIT_FOR_REC_TEXT_RESULTS:
                Rec_Text_Result asr_result = gson.fromJson(message, Rec_Text_Result.class);

                if(asr_result.message == "query_error")
                {
                    QueryError error_asr = gson.fromJson(message, QueryError.class);

                    // something failed, maybe because no audio was detected?
                    log.info("[MIX] voice recognition failed. reason: " + error_asr.reason);
                    this.mixStatus = MixConnectionStatus.CONNECTED;
                    break;
                }

                try {
                    log.info("[MIX] asr recognized: " + asr_result.transcriptions.get(0));
                }
                catch(Exception ex)
                {
                    log.info("[MIX] error while reading asr: " + ex.getMessage());
                }

                // currently we just do nothing with the text result
                this.mixStatus = MixConnectionStatus.WAIT_FOR_NLU_INTERPRETATION_RESULTS;
                break;

            case WAIT_FOR_NLU_INTERPRETATION_RESULTS:
                try {
                    ASR_NLU_Response nlu_result = gson.fromJson(message, ASR_NLU_Response.class);

                    if(nlu_result.nlu_interpretation_results != null
                            && nlu_result.nlu_interpretation_results.payload != null
                            && nlu_result.nlu_interpretation_results.payload.interpretations != null
                            && nlu_result.nlu_interpretation_results.payload.interpretations.size() > 0) {

                        // NLU intent detected!
                        Interpretation JSONIntent = nlu_result.nlu_interpretation_results.payload.interpretations.get(0);

                        if(JSONIntent.action.intent.confidence < 0.7)
                        {
                            // NLU to unsure
                            log.info("[MIX] intent detected but unsure! " + JSONIntent.action.intent.value + " (confidence: " + JSONIntent.action.intent.confidence + ")");
                            this.mixStatus = MixConnectionStatus.WAIT_FOR_QUERY_END;
                            return;
                        }

                        MixIntent mixIntent = new MixIntent();
                        List<MixConcepts> concepts = new ArrayList<MixConcepts>();

                        // Create Intent Object
                        mixIntent.concepts = concepts;
                        mixIntent.confidence = JSONIntent.action.intent.confidence;
                        mixIntent.intent = JSONIntent.action.intent.value;

                        // Output Entities as String for debugging purpose
                        String entitiesAsString = "";
                        for(MixConcepts var : mixIntent.concepts)
                            entitiesAsString = entitiesAsString + var.entity + ":"  + var.value + ", ";

                        if(entitiesAsString.length() > 3)
                            entitiesAsString = entitiesAsString.substring(0,entitiesAsString.length() - 2);

                        log.info("[MIX] intent detected! " + mixIntent.intent + " (confidence: " + mixIntent.confidence + ", entities: " + entitiesAsString + ")");
                        HandleIntent(mixIntent);
                    }
                    else
                    {
                        log.info("[MIX] no intent detected");
                    }
                }
                catch (Exception ex){
                    log.info("[MIX] error while reading nlu: " + ex.getMessage());
                }

                this.mixStatus = MixConnectionStatus.WAIT_FOR_QUERY_END;
                break;

            case WAIT_FOR_QUERY_END:
                this.mixStatus = MixConnectionStatus.CONNECTED;
                break;

            case CONNECTED:
                this.mixStatus = MixConnectionStatus.NOT_CONNECTED;
                break;

            default:
                break;
        }
    }

    /**
     * Gets called when the websocket connection to Nuance Mix is closed
     * @param userSession the userSession which is getting closed
     * @param reason the reason for connection close
     */
    @OnClose
    public void onClose(Session userSession, CloseReason reason) {
        log.info("[MIX] closing websocket");
        this.mixSession = null;
    }

    /**
     * Gets called when the websocket connection to Nuance Mix is opening
     * @param userSession
     */
    @OnOpen
    public void onOpen(Session userSession) {
        log.info("[MIX] opening websocket");
        this.mixSession = userSession;
    }
}
