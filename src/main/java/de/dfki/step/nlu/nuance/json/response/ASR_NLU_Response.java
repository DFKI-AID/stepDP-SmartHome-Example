package de.dfki.step.nlu.nuance.json.response;

public class ASR_NLU_Response {
    public String NMAS_PRFX_SESSION_ID;
    public String NMAS_PRFX_TRANSACTION_ID;
    public int final_response;
    public String message;
    public NLUInterpretationResults nlu_interpretation_results ;
    public String result_format;
    public String result_type;
    public int transaction_id;
    public String cadence_regulatable_result;
    public String prompt;
    public int status_code;
    public AudioTransferInfo audio_transfer_info;
}
