using System;

public class Class1
{
	public Class1()
	{
	}
    public static void Initialize()
    {
        // Tell the program we're busy initializing
        _ready = false;
        SplusInitializationCompleteFeedback(0);
        NotifyProgram("Initializing");

        //Initalize variables
        _pushBulletSocket = new WebSocketClient();
        _commandMessages = new Dictionary<int, PushBulletMessage>();
        _notificationMessages = new Dictionary<int, PushBulletMessage>();
        _critSect = new CCriticalSection();
        _sendData = new byte[6];

        _dataToSend = "Initializing";

        //Setup user information
        _activeUser = new PushBulletUser();

        //Setup websocket
        _pushBulletSocket.Port = 443;
        _pushBulletSocket.SSL = true;
        _pushBulletSocket.VerifyServerCertificate = false;
        _pushBulletSocket.KeepAlive = true;

        _pushBulletSocket.SendCallBack = OnSendCallback;
        _pushBulletSocket.ReceiveCallBack = OnReceiveCallback;
        _sendData = System.Text.Encoding.ASCII.GetBytes(_dataToSend);
        _receiveData = new byte[_sendData.Length];

        //Connect to the server
        ConnectToServer();
    }

    private static void ConnectToServer()
    {
        _pushBulletSocket.URL = "wss://stream.pushbullet.com/websocket/" + AccessToken;
        _connectionResult = _pushBulletSocket.Connect();
        _pushBulletSocket.KeepAlive = true;

        if (_pushBulletSocket.Connected)
        {
            Debug(AppConstants.DebugColorGreen, ">>>>>>>>>>>> Websocket Connected!!");
            SplusInitializationCompleteFeedback(1);
            GetUserInformation();
            StartPushBulletListener();
            _ready = true;
        }
        else
        {
            Debug(AppConstants.DebugColorRed, ">>>>>>>>>>>> Websocket did not connect=", _connectionResult.ToString());
        }
    }

    public static int OnReceiveCallback(byte[] data, uint datalen, WebSocketClient.WEBSOCKET_PACKET_TYPES opcode, WebSocketClient.WEBSOCKET_RESULT_CODES error)
    {
        Debug(AppConstants.DebugColorCyan, ".............. Callback Received.");
        try
        {
            string s = Encoding.UTF8.GetString(data, 0, data.Length);
            PushBulletType messageType = new PushBulletType();
            var jo = JObject.Parse(s);

            messageType.type = (string)jo["type"];
            messageType.subtype = (string)jo["subtype"];
            Debug(AppConstants.DebugColorCyan, ">>>>>>>>>>>> type=", messageType.type);
            Debug(AppConstants.DebugColorCyan, ">>>>>>>>>>>> subtype=", messageType.subtype);

            if (messageType.type == "tickle")
            {
                if (messageType.subtype == "push")
                {
                    GetPushInformation();
                }
            }
        }
        catch (Exception e)
        {
            Debug(AppConstants.DebugColorRed, ">>>>>>>>>>>> OnReceiveCallback failed=", e.ToString());
            //ErrorLog.Exception("PushBulletSystem> OnReceiveCallback() Exception:", e);
            return -1;
        }
        return 0;
    }

}
