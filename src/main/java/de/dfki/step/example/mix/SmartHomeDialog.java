package de.dfki.step.example.mix;

import de.dfki.step.core.ComponentManager;
import de.dfki.step.core.CoordinationComponent;
import de.dfki.step.core.TokenComponent;
import de.dfki.step.dialog.Dialog;
import de.dfki.step.output.PresentationComponent;
import de.dfki.step.rengine.RuleComponent;
import de.dfki.step.sc.SimpleStateBehavior;
import de.dfki.step.nlu.nuance.MixSpeechNLU;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.net.URISyntaxException;
import java.util.Collections;
import java.util.Objects;
import java.util.Set;

public class SmartHomeDialog extends Dialog {
    private static final Logger log = LoggerFactory.getLogger(SmartHomeDialog.class);
    public static MixSpeechNLU Mix;

    public SmartHomeDialog() {
        // Loading Model
        try {
            SmartHomeDialog.SmartHomeBehavior smartHomeBehavior = new SmartHomeDialog.SmartHomeBehavior();
            addComponent(smartHomeBehavior);
        } catch (URISyntaxException e) {
            e.printStackTrace();
        }

        // Starting Mix Service
        try {
            Mix = new MixSpeechNLU("0.0.0.0", 50001);
            Mix.StartService();
            //Mix.StartRecording();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }


    public static class SmartHomeBehavior extends SimpleStateBehavior {

        private RuleComponent rc;
        private TokenComponent tc;
        private CoordinationComponent cc;
        private PresentationComponent pc;

        public SmartHomeBehavior() throws URISyntaxException {
            super("/sc/smarthome");
        }

        @Override
        public void init(ComponentManager cm) {
            super.init(cm);
            rc = cm.retrieveComponent(RuleComponent.class);
            tc = cm.retrieveComponent(TokenComponent.class);
            cc = cm.retrieveComponent(CoordinationComponent.class);
            pc = cm.retrieveComponent(PresentationComponent.class);

            handleSmartHome();
        }

        @Override
        public Set<String> getActiveRules(String state) {
            //makes sure that rules are only activated in the given state
            if (Objects.equals(state, "Home")) {
                return Set.of("start_radio", "ask_temperature", "light_on", "light_off");
            }
            if (Objects.equals(state, "RadioPlayback")) {
                return Set.of("stop_radio", "Light_on", "Light_off");
            }
            if (Objects.equals(state, "TemperatureValue")) {
                return Set.of("set_temperature");
            }
            return Collections.EMPTY_SET;
        }

        public void handleSmartHome() {

            rc.addRule("start_radio", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "play_radio"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Play radio!");
                                stateHandler.fire("play");

                            }).attachOrigin(t);
                        });
            });

            rc.addRule("stop_radio", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "stop_radio"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Stop radio!");
                                stateHandler.fire("stop");

                            }).attachOrigin(t);
                        });
            });

            rc.addRule("ask_temperature", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "ask_temperature"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Ask to change temperature!");
                                stateHandler.fire("goodbye");

                            }).attachOrigin(t);
                        });
            });

            rc.addRule("set_temperature", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "value_temperature"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Temperature changed!");
                                stateHandler.fire("goodbye");

                            }).attachOrigin(t);
                        });
            });


            rc.addRule("light_on", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "ligt_on"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Light on!");
                            }).attachOrigin(t);
                        });
            });

            rc.addRule("light_off", () -> {
                tc.getTokens().stream()
                        .filter(t -> t.payloadEquals("intent", "light_off"))
                        .forEach(t -> {
                            cc.add(() -> {
                                System.out.println("Light off!");
                            }).attachOrigin(t);
                        });
            });
        }
    }
}
