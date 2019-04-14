using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TF5156
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private IMqttClient mqttClient;
        
        public MainWindow() {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e) {
            System.Console.WriteLine("OnContentRendered");

            // Create a new MQTT client.
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();

            mqttClient.Connected += (s, ev) => {
                Console.WriteLine("### CONNECTED WITH SERVER ###");
                PrintLog("连接成功!");
                ConnectBtn.Dispatcher.Invoke(new Action(() => {
                    SubscribeBtn.IsEnabled = true;
                    FireBtn.IsEnabled = true;
                    FaultBtn.IsEnabled = true;
                    SubscribeTF5156Btn.IsEnabled = true;
                    OfflineBtn.IsEnabled = true;
                    SubscribeLoraBtn.IsEnabled = true;
                    ConnectBtn.Content = "已连接";
                }));
            };

            mqttClient.Disconnected += (s, ev) => {
                ConnectBtn.Dispatcher.Invoke(new Action(() => {
                    SubscribeBtn.IsEnabled = false;
                    SubscribeTF5156Btn.IsEnabled = false;
                    SubscribeLoraBtn.IsEnabled = false;
                    FireBtn.IsEnabled = false;
                    OfflineBtn.IsEnabled = false;
                    FaultBtn.IsEnabled = false;
                    ConnectBtn.Content = "连接";
                }));
                PrintLog("已断开连接");
                Console.WriteLine("### DISCONNECTED FROM SERVER ###");
            };

            mqttClient.ApplicationMessageReceived += (s, ev) => {
                Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                Console.WriteLine($"+ Topic = {ev.ApplicationMessage.Topic}");
                Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(ev.ApplicationMessage.Payload)}");

                var result = "";
                for (int i = 0; i < ev.ApplicationMessage.Payload.Length; i++) {
                    result += (ev.ApplicationMessage.Payload[i] + " ");
                }
                Console.WriteLine($"+ QoS = {ev.ApplicationMessage.QualityOfServiceLevel}");
                Console.WriteLine($"+ Retain = {ev.ApplicationMessage.Retain}");
                Console.WriteLine();

                PrintLog("收到消息 topic:"+ ev.ApplicationMessage.Topic);
                PrintLog("消息内容 :\n" + result + "\n");
            };
        }

        protected override void OnClosing(CancelEventArgs e) {
            System.Console.WriteLine("OnClosing");
            if (mqttClient.IsConnected) {
                mqttClient.DisconnectAsync();
            }
        }

        private void PrintLog(string message) {
            LogTextBlock.Dispatcher.Invoke(new Action(() => {
                LogTextBlock.Text += (System.DateTime.Now + " " + message + "\n");
                LogTextBlock.ScrollToEnd();
            }));
        }

        private bool AutoScroll = true;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            // User scroll event : set or unset autoscroll mode
            if (e.ExtentHeightChange == 0) {   // Content unchanged : user scroll event
                if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight) {   // Scroll bar is in bottom
                    // Set autoscroll mode
                    AutoScroll = true;
                } else {   // Scroll bar isn't in bottom
                    // Unset autoscroll mode
                    AutoScroll = false;
                }
            }

            // Content scroll event : autoscroll eventually
            if (AutoScroll && e.ExtentHeightChange != 0) {   // Content changed and autoscroll mode set
                // Autoscroll
                (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight);
            }
        }

        private async void Button_Connect_Click(object sender, RoutedEventArgs e) {
            if (mqttClient.IsConnected){
                await mqttClient.DisconnectAsync();
                return;
            }

            if (InputServerName.Text.Trim().Length == 0) {
                MessageBox.Show("服务器地址不能为空!", "ERROR");
                return;
            }

            var portText = InputServerPort.Text;
            var port = 1883;
            if (portText.Length > 0) {
                port = int.Parse(portText);
            }

            var willMessage = new MqttApplicationMessage();
            willMessage.Topic = "tamefire/tf5156/offline/TF0001";
            willMessage.Payload = new byte[] { 0x01 };

            // Create TCP based options using the builder.
            var options = new MqttClientOptionsBuilder()
                .WithClientId("tf5156" + System.DateTime.Now)
                .WithTcpServer(InputServerName.Text)
                .WithCredentials(InputUsername.Text, InputPassword.Text)
                .WithCleanSession()
                .WithWillMessage(willMessage)
                .Build();
            PrintLog("开始连接:" + InputServerName.Text);
            await  mqttClient.ConnectAsync(options);
            PrintLog("成功连接到:" + InputServerName.Text);
        }

        private async void Button_Subscribe_TF5156_Click(object sender, RoutedEventArgs e)
        {
            var topic = "tamefire/tf5156/send/#";
            PrintLog("开始订阅所有5156数据");
            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
            PrintLog("订阅主题" + topic + "成功!");
            Console.WriteLine("### SUBSCRIBED ###");
        }

        private async void Button_Subscribe_Lora_Click(object sender, RoutedEventArgs e)
        {
            var topic = "tamefire/lora/send/#";
            PrintLog("开始订阅所有Lora数据");
            // Subscribe to a topic
            await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
            PrintLog("订阅主题" + topic + "成功!");
            Console.WriteLine("### SUBSCRIBED ###");
        }

        private async void Button_Subscribe_Click(object sender, RoutedEventArgs e) {
            var topic = InputSubscribeTopic.Text;
            if (topic.Length > 0) {
                PrintLog("开始订阅:"+topic);
                // Subscribe to a topic
                await mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic(topic).Build());
                PrintLog("订阅主题" + topic+ "成功!");
                Console.WriteLine("### SUBSCRIBED ###");
            }
        }

        private async void Button_Alert_Click(object sender, RoutedEventArgs e) {
            var topic = InputPublishTopic.Text;
            Button btn = (Button)sender;

            if (topic.Length > 0) {
                byte[] payload = new byte[] {
                        0x40, 0x40, 0x00, 0x01, 0x01, 0x01, //@@(2),业务流水号(2) 应答要返回  协议版本号(2)
                        0x00, 0x00, 0x09, 0x0b, 0x04, 0x11,//7-12 时间-包发送时间 秒/分/時/日/月/年(6)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //13-18 源地址(6)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //19-24 目的地址(6)
                        0x30, 0x00, 0x02, //25-26 应用数据单元长度len(2), 27-命令字节(1)
                        //-----------------数据 28-(28+ len)--------------------------------- 
                        0x02, 0x01, //信息体类型标志(1),信息体对象数目(1)
                        0x01, 0x01, 0x29, 0x01, 0x00, 0x01, 0x01, 0x02, 0x00, 0x00, 0x00,  ////系统类型标志(1) 系统地址(1),部件类型(1),部件地址(4),部件状态(2),说明(2)
                        0x00, (byte) 202, (byte) 228, (byte) 200, (byte) 235, (byte) 196, (byte) 163, (byte) 191, (byte) 233, 0x00, //说明(10)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, //说明(10)
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,       //说明(9)
                        0x01, 0x01, 0x00, 0x0B, 0x0C, 0x11, //秒(1)//分(1),//時(1),//日(1),//月(1),//年(1)
                        //------------------结束数据-------------------------------------------
                        0x00, 0x23, 0x23//检验(1),##(2)
                };
                if ("故障".Equals(btn.Content)) {
                    payload[36] = 0x04;
                    payload[37] = 0x00;
                }
                PrintLog("开始发送"+ btn.Content + "到:" + topic);
                MqttApplicationMessage msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(false)
                    .Build();

                await mqttClient.PublishAsync(msg);
                PrintLog("发送 " + btn.Content + " 成功!");
                Console.WriteLine("### PUBLISHED ###");
            }
        }

        private async void Button_Offline_Click(object sender, RoutedEventArgs e)
        {
            var topic = InputPublishTopic.Text;
            if (topic.Length > 0)
            {
                var deviceId =topic.Substring(topic.LastIndexOf("/"));
                topic = "tamefire/tf5156/offline" + deviceId;
                PrintLog("开始发送离线到:" + topic);
                MqttApplicationMessage msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(new byte[] { })
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag(false)
                    .Build();

                await mqttClient.PublishAsync(msg);
                PrintLog("发送离线成功!");
                Console.WriteLine("### PUBLISHED ###");
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Clear();
        }
    }
}
