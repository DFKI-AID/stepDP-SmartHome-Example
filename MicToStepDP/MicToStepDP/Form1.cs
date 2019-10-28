using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace StreamTestServer
{
    public partial class Form1 : Form
    {

        uPLibrary.Networking.M2Mqtt.MqttClient mqttclient;
        private static readonly HttpClient httpclient = new HttpClient();

        public Form1()
        {
            InitializeComponent();
        }

        StreamClient client;
        private void Button1_Click(object sender, EventArgs e)
        {
            if(client == null || client.isRunning == false)
            {
                client = new StreamClient();
                client.start();
            }
            else
            {
                client.stop();
                client = null;
            }
        }

        StreamServer server;
        private void Button2_Click(object sender, EventArgs e)
        {
            if (server == null)
            {
                server = new StreamServer();
                server.start();
            }
            else
            {
                server.stop();
                server = null;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (mqttclient != null)
                mqttclient.Disconnect();

            if (client != null)
                client.stop();

            if (server != null)
                server.stop();
        }

        // connect MQTT
        private void Button3_Click(object sender, EventArgs e)
        {
            mqttclient = new uPLibrary.Networking.M2Mqtt.MqttClient(textBox4.Text, Convert.ToInt32(textBox3.Text), false, null, null, uPLibrary.Networking.M2Mqtt.MqttSslProtocols.None);            
            mqttclient.Connect("MQTT Controller");
        }


        private void Button5_Click(object sender, EventArgs e)
        {
            mqttclient.Publish("voice/mic", (new System.Text.UTF8Encoding()).GetBytes("1"));
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            mqttclient.Publish("voice/mic", (new System.Text.UTF8Encoding()).GetBytes("0"));
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // send http request
            var values = new Dictionary<string, string>
            {
                { "mic", "close" }
            };

            var content = new FormUrlEncodedContent(values);
            httpclient.PostAsync("http://" + textBox1.Text + ":" + textBox5.Text + "/mix/voice/status", content);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // send http request
            var values = new Dictionary<string, string>
            {
                { "mic", "open" }
            };

            var content = new FormUrlEncodedContent(values);
            httpclient.PostAsync("http://" + textBox1.Text + ":" + textBox5.Text + "/mix/voice/status", content);
        }
    }
}
