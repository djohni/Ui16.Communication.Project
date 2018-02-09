using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;

namespace UiTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            
        }
        private string outData;
        private bool connected = false;
        private int oldValue = 0;
        private int newValue = 0;
        private WebSocket client;

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (!connected) //Connect
            {
                string content;
                using (var wc = new System.Net.WebClient())
                    content = wc.DownloadString($"http://192.168.1.104/socket.io/?t={(DateTime.Now - new DateTime(1970, 1, 1, 1, 0, 0)).Ticks / 10000}");
                var contentSplit = content.Split(':');
                string uri = $"ws://192.168.1.104/socket.io/{contentSplit[3]}/{contentSplit[0]}";
                
                string[][] inputStrings;
                client = new WebSocket(uri);
                client.OnOpen += (ss, ee) =>
                {
                    textBox1.AppendText($"OnOpen Event Listener{Environment.NewLine}");
                    StartAliveTimer();
                };
                client.OnError += (ss, ee) =>
                    Debugger.Break();
                client.OnMessage += (ss, ee) =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => { textBox1.AppendText($"IN: {ee.Data}{Environment.NewLine}"); }));
                        if (ee.Data == "1::")
                        {
                            outData =
                                $"3:::USERTIME^{(DateTime.Now - new DateTime(1970, 1, 1, 1, 0, 0)).Ticks / 10000}^-60";
                            client.Send(outData);
                            Invoke(new Action(() => textBox1.AppendText($"OUT: {outData}{Environment.NewLine}")));
                        }
                        else if (ee.Data == "2::")
                        {
                            client.Send("2::");
                            Invoke(new Action(() => textBox1.AppendText($"OUT: 2::{Environment.NewLine}")));
                        }
                        else if (ee.Data.StartsWith("3:::"))
                        {
                            inputStrings = ee.Data.Substring(4).Split(new []{'\n', '='}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('^')).ToArray();
                            if (inputStrings.First()[0] == "UPDATE_PLAYLIST")
                            {
                                string currentPlaylist = "";
                                for (int i = 1; i < inputStrings.Length; i++)
                                {
                                    if (inputStrings[i].Length > 1 && inputStrings[i][1] == "var.currentPlaylist")
                                    {
                                        currentPlaylist = inputStrings[i][2];
                                        break;
                                    }
                                }
                                client.Send("3:::MEDIA_GET_PLISTS");
                                Invoke(new Action(() => textBox1.AppendText($"OUT: 3:::MEDIA_GET_PLISTS{Environment.NewLine}")));
                                client.Send($"3:::MEDIA_GET_PLIST_TRACKS^{currentPlaylist}");
                                Invoke(new Action(() => textBox1.AppendText($"OUT: 3:::MEDIA_GET_PLIST_TRACKS^{currentPlaylist}{Environment.NewLine}")));
                            }
                            for (int i = 0; i < inputStrings.Length; i++)
                            {
                                if (inputStrings[i].Length > 1 && inputStrings[i][1] == "i.0.mix")
                                {
                                    oldValue = 1000 - (int)(double.Parse(inputStrings[i][2]) * 1000);
                                    newValue = 1000 - (int)(double.Parse(inputStrings[i][2]) * 1000);
                                    Invoke(new Action(() =>
                                    {
                                        vScrollBar1.Value = newValue;
                                    }));
                                    break;
                                }
                            }
                            for (int i = 0; i < 3; i++)
                            {
                                if (oldValue != newValue)
                                {
                                    outData =
                                        $"3:::SETD^i.0.mix^{1.0 - newValue / 1000.0}";
                                    client.Send(outData);
                                    Invoke(new Action(() => textBox1.AppendText($"OUT: {outData}{Environment.NewLine}")));
                                    oldValue = newValue;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            
                        }
                    }
                };
                    
                client.OnClose += (ss, ee) =>
                {
                    tokenSource.Cancel();
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => { textBox1.AppendText($"OnClose Event Listener{Environment.NewLine}"); }));
                    }
                };
                client.Connect();
                textBox1.AppendText($"Connected to {uri}{Environment.NewLine}");
                ConnectButton.Text = "Disconnect";
                connected = true;
            }
            else //Disconnect
            {
                client.Close();
                ConnectButton.Text = "Connect";
                connected = false;
            }
        }

        private Stopwatch sw;
        private CancellationTokenSource tokenSource;
        private CancellationToken cancellationToken;
        private async Task StartAliveTimer()
        {
            tokenSource = new CancellationTokenSource();
            cancellationToken = tokenSource.Token;
            await Task.Factory.StartNew(() =>
            {
                if (sw == null) sw = new Stopwatch();
                else sw.Reset();
                sw.Start();
                double elapsedMilliseconds = sw.Elapsed.TotalMilliseconds;
                while (true)
                {
                    if(tokenSource.IsCancellationRequested)
                        break;
                    if (sw.Elapsed.TotalMilliseconds - elapsedMilliseconds >= 1000)
                    {
                        elapsedMilliseconds = sw.Elapsed.TotalMilliseconds;
                        if (!tokenSource.IsCancellationRequested)
                            client.SendAsync("3:::ALIVE", b => Invoke(new Action(() => { textBox1.AppendText($"OUT ({b}): 3:::ALIVE{Environment.NewLine}"); })));
                    }
                }
            }, cancellationToken);
        }

        private void vScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            newValue = vScrollBar1.Value;
        }
    }
}
