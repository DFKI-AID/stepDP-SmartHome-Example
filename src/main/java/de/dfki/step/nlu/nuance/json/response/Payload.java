package de.dfki.step.nlu.nuance.json.response;

import java.util.List;

public class Payload {
    public DiagnosticInfo diagnostic_info;
    public List<Interpretation> interpretations;
    public String type;
}
