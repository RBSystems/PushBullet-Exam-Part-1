using System;
using System.Text;
using Crestron.SimplSharp;              // For Basic SIMPL# Classes
using Crestron.SimplSharp.Net.Https;    // For access to HTTPS
using Crestron.SimplSharp.Net;          // For access to HTTPS
using Crestron.SimplSharp.CrestronWebSocketClient;
using Newtonsoft.Json;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json.Linq;
#pragma warning disable 0168

namespace PushbulletProcessor
{

    public delegate void PushbulletEventHandler(PushbulletEventArgs e);

    public class PushbulletEventArgs : EventArgs
    {
        public string message { get; set; }

        public PushbulletEventArgs()
        {
        }

        public PushbulletEventArgs(string Message)
        {
            this.message = Message;
        }
    }

    public class PushEvents
    {
        private static CCriticalSection myCriticalSection = new CCriticalSection();
        private static CMutex myMutex = new CMutex();
        public static event PushbulletEventHandler onPushReceived;

        public void PushbulletMessage(string Message)
        {
            PushEvents.onPushReceived(new PushbulletEventArgs(Message));
        }
    }


    /// <summary>
    /// This Class is used to Send Pushes
    /// </summary>
    public class Pushbullet
    {
        public WebSocketClient wsc = new WebSocketClient();
        public WebSocketClient.WEBSOCKET_RESULT_CODES ret;
        public WebSocketClient.WEBSOCKET_PACKET_TYPES opcode;
        String DataToSend = "abc123";
        public byte[] SendData = new byte[6];
        public String strc = "abc123";
        WebSocketClient.WEBSOCKET_RESULT_CODES wrc;
        public byte[] ReceiveData;
        public static string Access_Token;
        public static string Sender_Email;
        public static string Sender_Iden;
        private static CCriticalSection myCC = new CCriticalSection();

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Pushbullet()
        {
            wsc.Port = 443;
            wsc.SSL = true;
            wsc.KeepAlive = true;
            wsc.SendCallBack = SendCallback;
            wsc.ReceiveCallBack = ReceiveCallback;
            SendData = System.Text.Encoding.ASCII.GetBytes(DataToSend);
            ReceiveData = new byte[SendData.Length];
        }

        
        public short PushReceived(string Message)
        {
            PushEvents PE = new PushEvents();
            PE.PushbulletMessage(Message);

            return 1;
        }
         
        /// <summary>
        /// Class For Note Push in JSON Format. 
        /// </summary>
        public class Note
        {
            public string type{get; set;}
            public string title { get; set; }
            public string body {get; set;}
            public string email { get; set; }
        }


        /// <summary>
        /// Class for Device in JSON Format.
        /// </summary>
        public class Device
        {
            public string name { get; set; }
            public string model { get; set; }
            public string manufacturer { get; set; }
            public string icon { get; set;}
            public string iden { get; set; }
            public string email { get; set; }
            public string created { get; set; }
            public string modified { get; set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public class Dismiss
        {
            public bool dismissed { get; set; }
        }               

        /// <summary>
        /// Set the Access Token
        /// </summary>
        public void setAccessToken(string access_token)
        {
            Access_Token = access_token;
        }

        public void setSenderEmail(string sender_Email)
        {
            Sender_Email = sender_Email;
        }

        public void setSenderIden(string sender_Iden)
        {
            Sender_Iden = sender_Iden;
        }

        /// <summary>
        /// Connect to the Pushbullet Websocket to monitor for Pushes
        /// </summary>
        public void connect()
        {
            wsc.URL = "wss://stream.pushbullet.com/websocket/" + Access_Token;
            wrc = wsc.Connect();
            wsc.KeepAlive = true;
            
            if(wsc.Connected)
            //if (wrc == (int)WebSocketClient.WEBSOCKET_RESULT_CODES.WEBSOCKET_CLIENT_SUCCESS)
            {
                CrestronConsole.PrintLine("Websocket connected \r\n");
                getUserInfo();
            }
            else
            {
                CrestronConsole.Print("Websocket could not connect to server.  Connect return code: " + wrc.ToString());
            }
            //UpdateDevice(Sender_Iden, "MyProcessor", "CP3", "Crestron");
        }

        /// <summary>
        /// Disconnect from the Pushbullet Websocket
        /// </summary>
        public void Disconnect()
        {
            wsc.Disconnect();
            CrestronConsole.PrintLine("Websocket disconnected. \r\n");
        }

        /// <summary>
        /// The Following Three Methods are for the Websocket Listener
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public int SendCallback(WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            CrestronConsole.PrintLine("SendCallBack\r\n");
            try
            {
                ret = wsc.ReceiveAsync();
                //CrestronConsole.Print("SendCallBack : " + ret + "\n");
            }
            catch (Exception e)
            {
                return -1;
            }

            return 0;
        }
        public int ReceiveCallback(byte[] data, uint datalen, WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            CrestronConsole.PrintLine("ReceiveCallBack\r\n");
            try
            {
                string s = Encoding.UTF8.GetString(data, 0, data.Length);
                //CrestronConsole.Print("ReceiveCallback : " + s + "\n");
                if (s.Contains("push"))
                {
                    getPush();

                }
                else CrestronConsole.PrintLine("--- : {0}\n",s);
                //ErrorLog.Notice(s);

            }
            catch (Exception e)
            {
                return -1;
            }
            return 0;
        }

        public void AsyncSendAndReceive()
        {
            try
            {
                wsc.SendAsync(SendData, (uint)SendData.Length, WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME, WebSocketClient.WEBSOCKET_PACKET_SEGMENT_CONTROL.WEBSOCKET_CLIENT_PACKET_END);
            }
            catch (Exception e)
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Send Note Push
        /// </summary>
        /// <param name="Email"></param>
        /// <param name="Title"></param>
        /// <param name="Body"></param>
        public void sendNote(string Email, string Title, string Body)
        {
            string commandstring = "";
            if (Access_Token != "")
            {
                Note note = new Note
                {
                    type = "note",
                    title = Title,
                    body = Body,
                    email = Email
                };
                commandstring = JsonConvert.SerializeObject(note, Formatting.Indented);
                Send(commandstring);
            }
        }


        /// <summary>
        /// List Devices
        /// </summary>
        public void ListDevice()
        {
            if (Access_Token != "")
            {
                MyDevice();
            }
        }

        /// <summary>
        /// Create a Device
        /// </summary>
        /// <param name="Nickname"></param>
        /// <param name="Model"></param>
        /// <param name="Manufacturer"></param>
        public void CreateDevice(string Nickname, string Model, string Manufacturer)
        {
               string devicestring ="";
                if(Access_Token != "")
                {
                    Device device = new Device
                    {
                        name = Nickname,
                        model = "CP3",
                        manufacturer = "Crestron",
                        icon = "system"
                    };
                    devicestring = JsonConvert.SerializeObject(device, Formatting.Indented);
                    MyDevice(devicestring);
                }
        }

        public void UpdateDevice(string Iden, string Nickname, string Model, string Manufacturer)
        {
            string devicestring = "";
            if (Access_Token != "")
            {
                Device device = new Device
                {
                    name = Nickname,
                    model = "CP3",
                    manufacturer = "Crestron",
                    icon = "system"
                };
                devicestring = JsonConvert.SerializeObject(device, Formatting.Indented);
                MyDevice(Iden,devicestring);
            }
        }

            
        /// <summary>
        ///  This is Sent On Push
        /// </summary>
        /// <param name="commandstring"></param>
        public void Send(string commandstring)
        {
            HttpsClient client = new HttpsClient();
            client.PeerVerification = false;
            client.HostVerification = false;
            client.Verbose = false;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "https://api.pushbullet.com/v2/pushes";

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = Access_Token;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                request.ContentString = commandstring;
                response = client.Dispatch(request);

                if (response.Code >= 200 && response.Code < 300)
                {
                    // A response code between 200 and 300 means it was successful.
                    //CrestronConsole.Print(response.ContentString.ToString());
                    string s = response.ContentString.ToString();
                    //CrestronConsole.Print(s);
                    string[] words = s.Split(',');
                    string PushIden = "";
                  //  string PushTitle = "";
                   // string PushMessage = "";
                   // bool PushUnread = true;
                 //   bool SentMessage = false;
                    foreach (string word in words)
                    {
                        CrestronConsole.PrintLine(word + "\n");
                        if (word.Contains("\"iden\""))
                        {
                            PushIden = word.Substring(8, word.Length - 9);
                            //CrestronConsole.PrintLine("Look That: "+word.Substring(8, word.Length - 9)+"\n");
                        }
                        //ErrorLog.Notice(word + "\n");
                       // if (word.Contains("\"iden\""))
                       // {
                      //      PushIden = word.Substring(8, word.Length - 9);
                     //   }
                    }


                }
                else
                {
                    // A reponse code outside this range means the server threw an error.
                    ErrorLog.Notice("Pushbullet https response code: " + response.Code);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("Exception in Pushbullet: " + e.ToString());
            }
        }

        /// <summary>
        /// Create a Device
        /// </summary>
        /// <param name="devicestring"></param>
        public void MyDevice(string iden,string devicestring)
        {
            HttpsClient client = new HttpsClient();
            client.PeerVerification = false;
            client.HostVerification = false;
            client.Verbose = false;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "https://api.pushbullet.com/v2/devices/" + iden;

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = Access_Token;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                request.ContentString = devicestring;
                response = client.Dispatch(request);

                if (response.Code >= 200 && response.Code < 300)
                {
                    // A response code between 200 and 300 means it was successful.
                    //CrestronConsole.Print(response.ContentString.ToString());
                    string s = response.ContentString.ToString(); 
                    string[] words = s.Split(',');
                    foreach (string word in words)
                    {
                        CrestronConsole.PrintLine(word + "\n");
                    }
                }
                else
                {
                    // A reponse code outside this range means the server threw an error.
                    CrestronConsole.Print("Pushbullet https response code: " + response.Code);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("Exception in Pushbullet: " + e.ToString());
            }
        }

        public void MyDevice(string devicestring)
        {
            HttpsClient client = new HttpsClient();
            client.PeerVerification = false;
            client.HostVerification = false;
            client.Verbose = false;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "https://api.pushbullet.com/v2/devices";

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = Access_Token;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                request.ContentString = devicestring;
                response = client.Dispatch(request);

                if (response.Code >= 200 && response.Code < 300)
                {
                    // A response code between 200 and 300 means it was successful.
                    //CrestronConsole.Print(response.ContentString.ToString());
                    string s = response.ContentString.ToString();
                    string[] words = s.Split(',');
                    foreach (string word in words)
                    {
                        CrestronConsole.PrintLine(word + "\n");
                    }
                }
                else
                {
                    // A reponse code outside this range means the server threw an error.
                    CrestronConsole.Print("Pushbullet https response code: " + response.Code);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("Exception in Pushbullet: " + e.ToString());
            }
        }
        /// <summary>
        /// List Devices
        /// </summary>
        public void MyDevice()
        {
            HttpsClient client = new HttpsClient();
            client.PeerVerification = false;
            client.HostVerification = false;
            client.Verbose = false;

            HttpsClientRequest request = new HttpsClientRequest();
            HttpsClientResponse response;
            String url = "https://api.pushbullet.com/v2/devices";

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = Access_Token;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                //request.ContentString = devicestring;
                response = client.Dispatch(request);
                client.Abort();

                if (response.Code >= 200 && response.Code < 300)
                {
                    // A response code between 200 and 300 means it was successful.
                    string s = response.ContentString.ToString();
                    string[] words = s.Split(',');
                    foreach (string word in words)
                    {
                        CrestronConsole.PrintLine(word + "\n");
                    }
                }
                else
                {
                    // A reponse code outside this range means the server threw an error.
                    CrestronConsole.Print("Pushbullet https response code: " + response.Code);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error("Exception in Pushbullet: " + e.ToString());
            }
        }



        public ushort dismissPush(string PushIden)
        {
            if (Access_Token != "")
            {
                string commandstring = "";
                HttpsClient client = new HttpsClient();
                client.PeerVerification = false;
                client.HostVerification = false;
                client.Verbose = false;

                HttpsClientRequest request = new HttpsClientRequest();
                HttpsClientResponse response;
                String url = "https://api.pushbullet.com/v2/pushes/" + PushIden;

                request.KeepAlive = true;
                request.Url.Parse(url);
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);

                Dismiss dismiss = new Dismiss
                {
                    dismissed = true
                };
                commandstring = JsonConvert.SerializeObject(dismiss, Formatting.Indented);
                request.ContentString = commandstring;
                response = client.Dispatch(request);
                
                if (response.Code >= 200 && response.Code < 300)
                {
                    return 1;
                }
                else
                {
                    ErrorLog.Notice("Error Dismissing - " + response.Code.ToString() + "\n");
                    return 0;
                }
            }
            else

                return 0;
        }

        public ushort deletePush(string PushIden)
        {
            if (Access_Token != "")
            {
                HttpsClient client = new HttpsClient();
                client.PeerVerification = false;
                client.HostVerification = false;
                client.Verbose = false;
                client.UserName = Access_Token;
                

                HttpsClientRequest request = new HttpsClientRequest();
                HttpsClientResponse response;
                //String url = "https://api.pushbullet.com/v2/pushes/" + PushIden;
                String url = "https://api-pushbullet-com-fqp420kzi8tw.runscope.net/v2/pushes/" + PushIden;

                //request.KeepAlive = true;
                request.Url.Parse(url);
                request.Header.SetHeaderValue("Content-Type", "application/json");
                //request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Delete;
                request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);

                response = client.Dispatch(request);
                string s = response.ContentString.ToString();
                if (response.Code >= 200 && response.Code < 300)
                {
                    //ErrorLog.Notice("Deleted\n");
                    return 1;
                }
                else
                {
                    ErrorLog.Notice("Error Deleting - " + response.Code.ToString() + "\n");
                    return 0;
                }
            }
            else

                return 0;
        }

        /// <summary>
        /// This Method Will Retrieve all pushes since x.
        /// </summary>
        /// <returns></returns>
        public ushort getPush()
        {            
            string commandstring = "";
            if (Access_Token != "")
            {
                HttpsClient client = new HttpsClient();
                client.PeerVerification = false;
                client.HostVerification = false;
                client.Verbose = false;

                HttpsClientRequest request = new HttpsClientRequest();
                HttpsClientResponse response;
                String url = "https://api.pushbullet.com/v2/pushes";

                try
                {
                    myCC.Enter(); // Will not finish, until you have the Critical Section
                    request.KeepAlive = true;
                    request.Url.Parse(url);
                    request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                    request.Header.SetHeaderValue("Content-Type", "application/json");
                    request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                    request.ContentString = commandstring;


                    // Dispatch will actually make the request with the server
                    response = client.Dispatch(request);
                    client.Abort();
                    if (response.Code >= 200 && response.Code < 300)
                    {
                        string s = response.ContentString.ToString();
                        //ErrorLog.Notice(s + "\n");
                        string[] words = s.Split(',');
                        string PushIden = "";
                        string PushTitle = "";
                        string PushMessage = "";
                        bool PushUnread = true;
                        bool SentMessage = false;
                        foreach (string word in words)
                        {
                            //ErrorLog.Notice(word + "\n");
                            if (word.Contains("\"iden\""))
                            {
                                PushIden = word.Substring(8, word.Length - 9);
                            }
                            if (word.Contains("title"))
                            {
                                PushTitle = word.Substring(9, word.Length - 10);
                                if (PushTitle.Contains("\""))
                                {
                                    PushTitle.Substring(0, PushTitle.Length - 1);
                                }
                            }
                            if (word.Contains("body"))
                            {
                                if (word.Contains("}"))
                                {
                                    PushMessage = word.Substring(8, word.Length - 10);
                                }
                                else
                                {
                                    PushMessage = word.Substring(8, word.Length - 9);
                                    if (PushMessage.Contains("\""))
                                    {
                                        PushMessage.Substring(0, PushMessage.Length - 1);
                                    }
                                }
                            }
                            if (word.Contains("dismissed"))
                            {
                                if (word.Contains("true"))
                                {
                                    PushUnread = false;
                                    continue;
                                }
                            }
                            if (word.Contains("sender_email") && word.Contains(Sender_Email))
                            {
                                SentMessage = true;
                            }
                            if (word.Contains("}"))
                            {
                                //TODO Trigger Event To Output String to S+
                                if (PushTitle != "" || PushMessage != "")
                                {
                                    if (PushUnread == true)
                                    {
                                        if (SentMessage != true)
                                        {
                                            PushReceived("CMD=" + PushTitle + "." + PushMessage + ";");
                                        }
                                        ushort result = dismissPush(PushIden);
                                    }                                    
                                }
                                PushIden = "";
                                PushTitle = "";
                                PushMessage = "";
                                PushUnread = false;
                            }
                        }
                        //ErrorLog.Notice(response.ContentString.ToString());
                        // A response code between 200 and 300 means it was successful.
                        return 1;
                    }
                    else
                    {
                        ErrorLog.Notice("Response Code = " + response.Code.ToString() + "\n");
                        // A reponse code outside this range means the server threw an error.
                        return 0;
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Error("Exception in Pushbullet - GetInfo: " + e.ToString());
                    return 0;
                }
                finally
                {
                    myCC.Leave();
                }
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// https://api.pushbullet.com/v2/users/me
        /// </summary>
        /// <returns></returns>
        public ushort getUserInfo()
        {
            string commandstring = "";
            if (Access_Token != "")
            {
                HttpsClient client = new HttpsClient();
                client.PeerVerification = false;
                client.HostVerification = false;
                client.Verbose = false;

                HttpsClientRequest request = new HttpsClientRequest();
                HttpsClientResponse response;
                String url = "https://api.pushbullet.com/v2/users/me";

                try
                {
                    myCC.Enter(); // Will not finish, until you have the Critical Section
                    request.KeepAlive = true;
                    request.Url.Parse(url);
                    request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                    request.Header.SetHeaderValue("Authorization", "Bearer " + Access_Token);
                    request.ContentString = commandstring;

                    // Dispatch will actually make the request with the server
                    response = client.Dispatch(request);
                    client.Abort();
                    if (response.Code >= 200 && response.Code < 300)
                    {
                        string s = response.ContentString.ToString();
                        string[] words = s.Split(',');
                        Device senderDevice = new Device();
                        //ErrorLog.Notice(s + "\n");
                        //string senderEmail;
                        //string senderIden;
                        //string senderCreated;
                        //string senderModified;
                        //string senderName;

                        foreach (string word in words)
                        {
                            if (word.Contains("\"iden\""))
                            {
                                senderDevice.iden = word.Substring(8, word.Length - 9);
                                CrestronConsole.PrintLine("MYIden : "+senderDevice.iden+"\n");
                            }
                            else if (word.Contains("\"email\""))
                            {
                                senderDevice.email = word.Substring(9, word.Length - 10);
                                CrestronConsole.PrintLine("MYEmail : " + senderDevice.email + "\n");
                                setSenderEmail(senderDevice.email);
                            }
                            else if (word.Contains("\"name\""))
                            {
                                senderDevice.name = word.Substring(8, word.Length - 9);
                                CrestronConsole.PrintLine("MYName : "+senderDevice.name+"\n");
                            }
                            else if (word.Contains("\"created\""))
                            {
                                senderDevice.created = word.Substring(10, word.Length - 11);
                                CrestronConsole.PrintLine("MYCreated : "+senderDevice.created+"\n");
                            }
                            else if (word.Contains("\"modified\""))
                            {
                                senderDevice.modified = word.Substring(11, word.Length - 12);
                                CrestronConsole.PrintLine("MYModified : " + senderDevice.modified + "\n");
                            }
                        }

                        CrestronConsole.Print(response.ContentString.ToString());
                        // A response code between 200 and 300 means it was successful.
                        return 1;
                    }
                    else
                    {
                        ErrorLog.Notice("Response Code = " + response.Code.ToString() + "\n");
                        // A reponse code outside this range means the server threw an error.
                        return 0;
                    }
                }
                catch (Exception e)
                {
                    ErrorLog.Error("Exception in Pushbullet - GetInfo: " + e.ToString());
                    return 0;
                }
                finally
                {
                    myCC.Leave();
                }
            }
            else
            {
                return 0;
            }
        }




 
    }
}