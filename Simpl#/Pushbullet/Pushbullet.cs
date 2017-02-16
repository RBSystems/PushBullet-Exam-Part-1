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
        public ushort connect {get;set;}

        public PushbulletEventArgs()
        {
        }

        public PushbulletEventArgs(string Message)
        {
            this.message = Message;
        }

        public PushbulletEventArgs(ushort Connect)
        {
            this.connect = Connect;
        }
    }
    
    public class PushEvents
    {
        private static CCriticalSection myCriticalSection = new CCriticalSection();
        private static CMutex myMutex = new CMutex();
        public static event PushbulletEventHandler onPushReceived;
        public static event PushbulletEventHandler onPushConnected;

        public void PushbulletMessage(string Message)
        {
            PushEvents.onPushReceived(new PushbulletEventArgs(Message));
        }
        public void PushBulletConnect(ushort Connect)
        {
            PushEvents.onPushConnected(new PushbulletEventArgs(Connect));
        }
    }


    /// <summary>
    /// This Class is used to Send Pushes
    /// </summary>
    public class Pushbullet
    {
        public WebSocketClient   PBSocket = new WebSocketClient();
        public WebSocketClient.WEBSOCKET_RESULT_CODES PBSocketResult;
        public WebSocketClient.WEBSOCKET_PACKET_TYPES PBSocketOpCode;

        //Used Locally once Modified by S+

        private string _name = "MyProcessor";
        private string _model = "CP3";
        private string _iden = "";
        private string _email = "me@me.me";
        private string _token = "";
        private string Created = "";


        public string DeviceName
        {
            get { return _name; }
            set { _name = value; }
        }

        public string DeviceModel
        {
            get { return _model; }
            set { _model = value; }
        }

        public string DeviceIden
        {
            get { return _iden; }
            //set { _iden = value; }
        }

        private string DeviceEmail
        {
            get { return _email; }
            set { _email = value; }
        }

        private string DeviceToken
        {
            get { return _token; }
            set { _token = value; }
        }


        String DataToSend = "gold16";
        public byte[] SendData = new byte[6];
        public byte[] ReceiveData;
        WebSocketClient.WEBSOCKET_RESULT_CODES PBSocketResultCode;
        private static CCriticalSection PBSocketCC = new CCriticalSection();

            

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Pushbullet()
        {
            ReceiveData = new byte[SendData.Length];
            PBSocket.Port = 443;
            PBSocket.SSL = true;
            PBSocket.KeepAlive = true;
            PBSocket.SendCallBack = _SendCallback;
            PBSocket.ReceiveCallBack = _ReceiveCallback;
            SendData = System.Text.Encoding.ASCII.GetBytes(DataToSend);
        }





        
        public short PushReceived(string Message)
        {
            PushEvents PE = new PushEvents();
            PE.PushbulletMessage(Message);

            return 1;
        }

        public short PushConnected(ushort Connect)
        {
            PushEvents PE = new PushEvents();
            PE.PushBulletConnect(Connect);
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
            public string nickname { get; set; }
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
        public void SetDeviceToken(string access_token)
        {
            DeviceToken = access_token;
        }

        public void setDeviceEmail(string sender_Email)
        {
            DeviceEmail = sender_Email;
        }

        /// <summary>
        /// Connect to the Pushbullet Websocket to monitor for Pushes
        /// </summary>
        public void connect()
        {
            PBSocket.URL = "wss://stream.pushbullet.com/websocket/" + DeviceToken;
            PBSocketResultCode = PBSocket.Connect();
            PBSocket.KeepAlive = true;

            if (PBSocket.Connected)
            {
                CrestronConsole.PrintLine("Websocket connected \r\n");
                PushConnected(1);
            }
            else
            {
                CrestronConsole.Print("Websocket could not connect to server.  Connect return code: " + PBSocketResultCode.ToString());
                PushConnected(0);
            }
            getUserInfo();
            
            //UpdateDevice();
            //UpdateDevice(Sender_Iden, "MyProcessor", "CP3", "Crestron");
        }

        /// <summary>
        /// Disconnect from the Pushbullet Websocket
        /// </summary>
        public void Disconnect()
        {
            PBSocket.Disconnect();
            CrestronConsole.PrintLine("Websocket disconnected. \r\n");
            PushConnected(0);
        }

        /// <summary>
        /// The Following Three Methods are for the Websocket Listener
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
       public int _SendCallback(WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
//            CrestronConsole.PrintLine("SendCallBack\r\n");
            try
            {
                PBSocketResult = PBSocket.ReceiveAsync();
                //CrestronConsole.Print("SendCallBack : " + ret + "\n");
            }
            catch (Exception e)
            {
                return -1;
            }

            return 0;
        }

        public int _ReceiveCallback(byte[] data, uint datalen, WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES error)
        {
            //CrestronConsole.PrintLine("ReceiveCallBack\r\n");
            try
            {
                string s = Encoding.UTF8.GetString(data, 0, data.Length);
                //CrestronConsole.Print("ReceiveCallback : " + s + "\n");
                if (s.Contains("push"))
                {
                    getPush();
                }
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
                PBSocket.SendAsync(SendData, (uint)SendData.Length, WebSocketClient.WEBSOCKET_PACKET_TYPES.LWS_WS_OPCODE_07__TEXT_FRAME, WebSocketClient.WEBSOCKET_PACKET_SEGMENT_CONTROL.WEBSOCKET_CLIENT_PACKET_END);
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
            if (DeviceToken != "")
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
 /*       public void ListDevice()
        {
            if (DeviceToken != "")
            {
                MyDevice();
            }
        }
*/
        /// <summary>
        /// Create a Device
        /// </summary>
       public void CreateDevice()
        {
            string devicestring = "";
            if (DeviceToken != "")
            {
                Device device = new Device
                {
                    nickname = DeviceName,
                    model = DeviceModel,
                    manufacturer = "Crestron",
                    iden = DeviceIden,
                    email = DeviceEmail,
                    icon = "system"
                };
                devicestring = JsonConvert.SerializeObject(device, Formatting.Indented);
                MyDevice(devicestring);
            }
        }

        /// <summary>
        /// Update a Device
        /// </summary>
        public void UpdateDevice()
        {
            string devicestring = "";
            if (DeviceToken != "")
            {
                Device device = new Device
                {
                    nickname = DeviceName,
                    model = DeviceModel,
                    email = DeviceEmail,
                    icon = "system"
                };

                devicestring = JsonConvert.SerializeObject(device, Formatting.Indented);
                MyDevice(DeviceIden,devicestring);
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
                client.UserName = DeviceToken;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
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
                        //CrestronConsole.PrintLine(word + "\n");
                        if (word.Contains("\"iden\""))
                        {
                            PushIden = word.Substring(8, word.Length - 9);
                            //CrestronConsole.PrintLine("Look That: "+word.Substring(8, word.Length - 9)+"\n");
                        }
                        if (word.Contains("\"created\""))
                        {
                            Created = word.Substring(10, word.Length - 10);
                            //CrestronConsole.PrintLine("Look That: "+word.Substring(10, word.Length - 10)+"\n");
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
        /// Update a Device
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
            String url = "https://api.pushbullet.com/v2/devices/"+iden;

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = DeviceToken;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
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
                        //CrestronConsole.PrintLine(word + "\n");
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
        /// Create a Device
        /// </summary>
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
                client.UserName = DeviceToken;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
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
                        //CrestronConsole.PrintLine(word + "\n");
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
            string MyDeviceName = "";
            bool MyDeviceActive = false;
            //CrestronConsole.PrintLine("MyDevice\n");

            try
            {
                request.KeepAlive = true;
                request.Url.Parse(url);
                client.UserName = DeviceToken;
                request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                request.Header.SetHeaderValue("Content-Type", "application/json");
                request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
                //request.ContentString = devicestring;
                response = client.Dispatch(request);
                client.Abort();

                if (response.Code >= 200 && response.Code < 300)
                {
                    // A response code between 200 and 300 means it was successful.
                    string s = response.ContentString.ToString();
                    bool MyDeviceFind = false;
                    string[] words = s.Split(',');
                    foreach (string word in words)
                    {
                        //CrestronConsole.PrintLine("Device : "+word + "\n");
                        if(word.Contains("{\"active\":true"))
                        {
                           //CrestronConsole.PrintLine("Device is active\n");
                           MyDeviceActive = true;
                        }
                        

                        if (word.Contains("\"nickname\"")&&MyDeviceActive==true)
                        {
                            MyDeviceName = word.Substring(12, word.Length - 13);
                            //CrestronConsole.PrintLine(MyDeviceName + " = " + DeviceName + " \n");

                            if (MyDeviceName == DeviceName)
                            {
                                MyDeviceFind = true;
                                //CrestronConsole.PrintLine(MyDeviceName + " Finded\n");
                                break;
                            }
                        } 
                        //CrestronConsole.PrintLine(word + "\n");
                    }
                    if (MyDeviceFind == false) CreateDevice();
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
        /// This Method Will dismissPush.
        /// </summary>
        /// <returns></returns>
        public ushort dismissPush(string PushIden)
        {
            if (DeviceToken != "")
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
                request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);

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
 

        /// <summary>
        /// This Method Will Retrieve all pushes since x.
        /// </summary>
        /// <returns></returns>
        public ushort getPush()
        {            
            string commandstring = "";
            string modified_after = Created;

            if (DeviceToken != "")
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
                    PBSocketCC.Enter(); // Will not finish, until you have the Critical Section
                    request.KeepAlive = true;
                    request.Url.Parse(url);
                    request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                    request.Header.SetHeaderValue("Content-Type", "application/json");
                    request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
                    commandstring = JsonConvert.SerializeObject(modified_after, Formatting.Indented);

                    request.ContentString = commandstring;


                    // Dispatch will actually make the request with the server
                    response = client.Dispatch(request);
                    client.Abort();
                    if (response.Code >= 200 && response.Code < 300)
                    {
                        string s = response.ContentString.ToString();
                        //CrestronConsole.Print("Get Response : " + s + "\n");
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
                            //CrestronConsole.Print("Get Response : " + word + "\n");
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
                           if (word.Contains("sender_email") && word.Contains(DeviceEmail))
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
                                            PushReceived("CMD=" + PushMessage);
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
                    PBSocketCC.Leave();
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



            if (DeviceToken != "")
            {
                HttpsClient client = new HttpsClient();
                client.PeerVerification = false;
                client.HostVerification = false;
                client.Verbose = false;

                HttpsClientRequest request = new HttpsClientRequest();
                HttpsClientResponse response;
                String url = "https://api.pushbullet.com/v2/users/me";
                //CrestronConsole.PrintLine("-------------GetUserInfo\n");

                try
                {
                    PBSocketCC.Enter(); // Will not finish, until you have the Critical Section
                    request.KeepAlive = true;
                    request.Url.Parse(url);
                    request.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                    request.Header.SetHeaderValue("Authorization", "Bearer " + DeviceToken);
                    request.ContentString = commandstring;

                    // Dispatch will actually make the request with the server
                    response = client.Dispatch(request);
                    client.Abort();
                    if (response.Code >= 200 && response.Code < 300)
                    {
                        string s = response.ContentString.ToString();
                        string[] words = s.Split(',');

                        foreach (string word in words)
                        {
                            if (word.Contains("\"iden\""))
                            {
                                _iden = word.Substring(8, word.Length - 9);
                               // CrestronConsole.PrintLine("MYIden : " + DeviceIden + "\n");
                                MyDevice();
                            }
                        }

                       // CrestronConsole.Print(response.ContentString.ToString());
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
                    PBSocketCC.Leave();
                }
            }
            else
            {
                return 0;
            }
        }




 
    }
}