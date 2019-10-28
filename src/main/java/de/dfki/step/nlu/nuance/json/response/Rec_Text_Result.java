package de.dfki.step.nlu.nuance.json.response;

import java.util.List;

public class Rec_Text_Result {
    public AudioTransferInfo audio_transfer_info;
    public String cadence_regulatable_result;
    public int final_response;
    public String message;
    public String NMAS_PRFX_SESSION_ID;
    public String NMAS_PRFX_TRANSACTION_ID;
    public String prompt;
    public String result_format;
    public String result_type;
    public int status_code;
    public int transaction_id;

    public List<List<Word>> words;
    public List<Integer> confidences;
    public List<String> transcriptions;
}
