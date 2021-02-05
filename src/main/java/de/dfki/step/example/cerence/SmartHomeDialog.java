package de.dfki.step.example.cerence;

import de.dfki.step.cerence.NLUController;
import de.dfki.step.dialog.Dialog;
import de.dfki.step.kb.semantic.Type;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

public class SmartHomeDialog extends Dialog {
    private static final Logger log = LoggerFactory.getLogger(SmartHomeDialog.class);
    public static de.dfki.step.cerence.NLUController cerence;

    public SmartHomeDialog() {
        // Adding types to KB
        try {
            Type type_start_radio = new Type("start_radio", this.getKB());
            Type type_stop_radio = new Type("stop_radio", this.getKB());
            Type type_ask_temperature = new Type("ask_temperature", this.getKB());
            Type type_set_temperature = new Type("set_temperature", this.getKB());
            Type type_light_on = new Type("light_on", this.getKB());
            Type type_light_off = new Type("light_off", this.getKB());

            this.getKB().addType(type_ask_temperature);
            this.getKB().addType(type_start_radio);
            this.getKB().addType(type_light_off);
            this.getKB().addType(type_light_on);
            this.getKB().addType(type_set_temperature);
            this.getKB().addType(type_stop_radio);
        } catch (Exception e) {
            e.printStackTrace();
        }

        // Starting Mix Service
        try {
            cerence = new NLUController(this);
            cerence.InitCerenceMix("wss://autotesting-azure-use2-cvt1-voice-ws.nuancemobility.net/v2",
                    "NMDPTRIAL_julian_wolter_dfki_de20190430095448",
                    "86376F1D3BF1805A1ED8C8CFE637463A9F40D99F195661BA9D5AAACC7E78AA081DCA18CF47AB8BC556133D2843ADCDA8A97CA4F3B7830B57AB3C2DB8DD1571DF",
                    "A556_C4721",
                    "eng-USA");
            cerence.StartAudioServer("0.0.0.0", 50002);
        } catch (Exception e) {
            e.printStackTrace();
        }
    }
}
