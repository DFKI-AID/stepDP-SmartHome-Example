## Function Principle

If the service is started, a TCP server opens and accepts connections. This servers receives the audio files, encoded in OPUS (16kHz, Mono).

In the background, a PipedInputStream is used to buffer the audio data. A separate worker thread copies the audio data from the TCP socket into the PipedInputStream.

After that, a PipedOutputStream is used to transfer the data to Nuance in a separate thread.