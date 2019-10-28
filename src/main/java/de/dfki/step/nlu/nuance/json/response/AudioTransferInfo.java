package de.dfki.step.nlu.nuance.json.response;

import java.util.List;

public class AudioTransferInfo {
    public int audio_id;
    public String end_time;
    public String nss_server;
    public String start_time;
    public List<AudioPackages> packages;
}
