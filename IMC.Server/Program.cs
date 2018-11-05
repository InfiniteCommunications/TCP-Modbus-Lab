using EasyModbus;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.SQLite;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// State object for reading client data asynchronously  
public class StateObject
{
    // Client  socket.  
    public Socket workSocket = null;
    // Size of receive buffer.  
    public const int BufferSize = 1024;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.  
    public StringBuilder sb = new StringBuilder();

}
public class AsynchronousSocketListener
{
    // Thread signal.  
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    private static SQLiteConnection sql_con;
    private static SQLiteCommand sql_cmd;
    private static SQLiteDataAdapter DB;

    private static DateTime now = DateTime.Now;

    //private static string ipAddress = "10.10.10.181";
    private static int port = 15800;

    private static List<string>  ipAddList = new List<string>();

    private static IPAddress ipAddr;

    //Modbus connections
    //private static ModbusClient modbusClient = new ModbusClient(ipAddress, 502); //Ip-Address and Port of Modbus-TCP-Server

    private static void SetConnection()
    {
        sql_con = new SQLiteConnection
            ("Data Source=serverData1.db;Version=3;New=False;Compress=True;");
    }
    private static void ExecuteQuery(string txtQuery)
    {
        SetConnection();
        sql_con.Open();
        sql_cmd = sql_con.CreateCommand();
        sql_cmd.CommandText = txtQuery;
        sql_cmd.ExecuteNonQuery();
        sql_con.Close();
    }
    public AsynchronousSocketListener()
    {
    }
    public static void StartListening()
    {
        // Establish the local endpoint for the socket.  
        // The DNS name of the computer  
        // running the listener is "host.contoso.com".  
        //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        //IPAddress ipAddress = ipHostInfo.AddressList[0];
        //IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

        //app.config codes
        /*List<string> ipAddrNPort = configFile();
        if (ipAddrNPort.Count <= 0)
        {
            exitOnPress();
        }
        //IPAddress ipAddress = IPAddress.Parse(ipAddrNPort[0]);*/

        Console.WriteLine(port);
        Console.WriteLine("Please Select your NIC IP to start the imc server :");
        IpAddList();
        
        int inputIpManual = Convert.ToInt32(Console.ReadLine());
        int ipListSize = ipAddList.Count;
        while (inputIpManual > ipListSize)
        {
            Console.WriteLine("Please Select your NIC IP to start the imc server :");
            IpAddList();
            inputIpManual = Convert.ToInt32(Console.ReadLine());
        }

        ipAddr = IPAddress.Parse(ipAddList[inputIpManual-1]);


        IPEndPoint localEndPoint = new IPEndPoint(ipAddr, port);

        // Create a TCP/IP socket.  
        Socket listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);

            Console.WriteLine("imc TCP listening service : " + ipAddr + " started succesful on port : " + port);

            while (true)
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(
                    new AsyncCallback(AcceptCallback),
                    listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

        Console.WriteLine("\nPress ENTER to continue...");
        Console.Read();

    }
    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.  
        allDone.Set();

        // Get the socket that handles the client request.  
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.  
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }
    public static void ReadCallback(IAsyncResult ar)
    {
        try
        {
            String content = String.Empty;

            //IniFileHelper
            IniFileHelper iniFile = new IniFileHelper();

            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket.   
            int bytesRead = handler.EndReceive(ar);  

            if (bytesRead > 0)
            {
            // There  might be more data, so store the data received so far.  
            state.sb.Append(Encoding.ASCII.GetString(
                state.buffer, 0, bytesRead));
           
                // Check for end-of-file tag. If it is not there, read   
                // more data.  
                content = state.sb.ToString();
                if (content.Contains("6"))
                {
                    string ReceivedOutput = null;

                    switch (content.Substring(0, 3))
                    {
                        case "611":
                            string device1Result = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                            string device2Result = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                            string resultOutput = null;
                            if (device1Result == "-1" && device2Result == "-1")
                            {
                                resultOutput = "0";
                            }
                            else
                            {
                                resultOutput = "1";
                            }

                            ReceivedOutput = "621" + now.ToString("HHmmss") + "015" + "|" + content.Substring(13, 6) + "|" + resultOutput + "|01|02|";

                            if (device1Result != "-1")
                            {
                                bool result = IniFileHelper.WriteValue("Device1", "status", " 1", System.IO.Path.GetFullPath("IniFile.ini"));
                            }
                            if (device2Result != "-1")
                            {
                                bool results = IniFileHelper.WriteValue("Device2", "status", " 1", System.IO.Path.GetFullPath("IniFile.ini"));
                            }
                            //modbusClient.Connect();
                            //modbusClient.WriteSingleCoil(0001, true);
                            //modbusClient.Disconnect();
                            break;
                        case "612":
                            string device1Reset = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                            string device2Reset = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                            string resetOutput1 = null;
                            string resetOutput2 = null;
                            if (device1Reset == "-1")
                            {
                                resetOutput1 = "1";
                            }
                            else
                            {
                                resetOutput1 = "0";
                            }

                            if (device2Reset == "-1")
                            {
                                resetOutput2 = "1";
                            }
                            else
                            {
                                resetOutput2 = "0";
                            }
                            ReceivedOutput = "622" + now.ToString("HHmmss") + "010" + "|" + content.Substring(13, 6) + "|" + resetOutput1 + "|" + resetOutput2 + "|";

                            if (device1Reset != "-1")
                            {
                                bool result = IniFileHelper.WriteValue("Device1", "status", "0", System.IO.Path.GetFullPath("IniFile.ini"));
                            }
                            if (device2Reset != "-1")
                            {
                                bool results = IniFileHelper.WriteValue("Device2", "status", "0", System.IO.Path.GetFullPath("IniFile.ini"));
                            }
                            //modbusClient.Connect();
                            //modbusClient.WriteSingleCoil(0001, false);
                            //modbusClient.Disconnect();
                            break;

                        case "619":
                            string device1Status = iniFile.ReadValue("Device1", "status", System.IO.Path.GetFullPath("IniFile.ini"));
                            string device2Status = iniFile.ReadValue("Device2", "status", System.IO.Path.GetFullPath("IniFile.ini"));

                            //modbusClient.Connect();
                            ////modbusClient.WriteSingleCoil(0001, false);
                            //bool[] status = modbusClient.ReadCoils(0001, 1);
                            //modbusClient.Disconnect();
                            ReceivedOutput = "629" + now.ToString("HHmmss") + "011" + "|" + content.Substring(13, 6) + "|" + device1Status + "|" + device2Status + "|";
                            break;
                        default:
                            ReceivedOutput = "invalid command code";
                            break;
                    }
                
                    // All the data has been read from the   
                    // client. Display it on the console.  
                    Console.WriteLine("Read {0} bytes from socket.\nData : {1}",
                        content.Length, content);

                    Message("Read "+content.Length+" bytes from socket.\nData : "+content+"");

                    // Echo the data back to the client.  
                    Send(handler, ReceivedOutput);
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    private static void Message(string msgInput)
    {
        DateTime now = DateTime.Now;
        string txtSQLQuery = "insert into  serverMsg1 (date_time , message) values ('"+ now + "', '"+msgInput+"')";
        ExecuteQuery(txtSQLQuery);
    }
    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.  
        byte[] byteData = Encoding.ASCII.GetBytes(data);

        // Begin sending the data to the remote device.  
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }
    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.  
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.  
            int bytesSent = handler.EndSend(ar);
            Console.WriteLine("Sent {0} bytes to client.", bytesSent);

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
    public static List<string> configFile()
    {
        List<string> ipAddNPort = new List<string>();
        try
        {
            var tcpServer = ConfigurationManager.GetSection("tcpServer") as NameValueCollection;

            if (tcpServer.Keys.Count == 0)
            {
                Console.WriteLine("Configuration File : No Device Found . Please check your XML config");
            }
            else
            {
                foreach (var key in tcpServer.AllKeys)
                {
                    ipAddNPort.Add(tcpServer[key]);
                    Console.WriteLine(tcpServer[key]);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Configuration File : Error . Please check your XML config");
        }
        return ipAddNPort;
    }
    private static void consoleResult(string output)
    {
        Console.WriteLine(output);
    }
    private static void ToLog(string input)
    {
        Logger logger = LogManager.GetLogger("Info");
        logger.Info(input);
    }


    public static void IpAddList()
    {  
        try
        {
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            int i = 1;
            foreach (IPAddress addr in localIPs)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    Console.WriteLine("[" + i + "] : " + addr);
                    ipAddList.Add(addr.ToString());
                    i++;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }

    }
    public static int Main(String[] args)
    {

        Console.WriteLine("Infinite Communication(s) PTE LTD");
        Console.WriteLine("Modbus IP Addres : 10.10.10.181");
        Console.WriteLine("Modbus Port : 502 \n");

        StartListening();
        return 0;
    }
    public static void exitOnPress()
    {
        var key = Console.ReadKey();
        Console.WriteLine("Press esc key to exit ...");
        if (key.Key == ConsoleKey.Escape)
        {
            Environment.Exit(0);
        }
    }
}