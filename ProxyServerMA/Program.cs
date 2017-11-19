using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProxyServerMA
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "Proxy Server MA";
            writeError("-------");

            int _waitPort = 0;
            int _port = 0;
            string _ip = "";
            #region Read Config
            var _configIp = ConfigurationManager.ConnectionStrings["ip"].ConnectionString;
            var _configPort = ConfigurationManager.ConnectionStrings["port"].ConnectionString;
            var _configServerPort = ConfigurationManager.ConnectionStrings["serverPort"].ConnectionString;

            if (_configIp != "")
            {
                _waitPort = Convert.ToInt32(_configServerPort);
                _port = Convert.ToInt32(_configPort);
                _ip = _configIp;

                _configIp = "";
                _configPort = "";
                _configServerPort = "";
            }
            #endregion

            while (true)
            {
                TcpClient _client = null;
                TcpListener _server = null;

                #region Read Ip Port, Connect Server
                do
                {
                    if (_ip == "")
                    {
                        Console.Write("Please write connect IP = ");
                        _ip = Console.ReadLine();
                        _port = getNumber("port");
                    }

                    try
                    {
                        _client = new TcpClient();
                        _client.Connect(_ip, _port);
                        Console.WriteLine("Connect ip with port...");
                        break;
                    }
                    #region Error
                    catch(Exception ex)
                    {
                        writeError("Ip connect error!");
                        clearSettings(out _port, out _ip, out _waitPort);
                    }
                    #endregion
                } while (true);
                _client.Close();
                #endregion

                #region Read Port, Active Server
                if (_waitPort == 0)
                    _waitPort = getNumber("server port");
                do
                {
                    try
                    {
                        _server = new TcpListener(IPAddress.Any, _waitPort);
                        _server.Start();
                        break;
                    }
                    #region Error
                    catch
                    {
                        writeError("Server open error!");
                        _waitPort = getNumber("server port");
                    }
                    #endregion
                } while (true);
                #endregion

                #region Sockets List
                List<Socket> _clients = new List<Socket>();
                List<TcpClient> _clientsServer = new List<TcpClient>();

                List<NetworkStream> _clientsNS = new List<NetworkStream>();
                List<NetworkStream> _clientsServerNS = new List<NetworkStream>();
                List<Thread> _connectThreads = new List<Thread>();
                #endregion
                bool _consoleDisable = true;

                #region Sockets Wait Thread
                Thread _waitThread = new Thread(
                    () =>
                    {
                        try
                        {
                            do
                            {
                                #region Accept Socket
                                if (!_consoleDisable)
                                    Console.WriteLine("Waiting accept tcp client...");
                                var _accept = _server.AcceptSocket();
                                _clients.Add(_accept);
                                if (!_consoleDisable)
                                    Console.WriteLine("Accept tcp client...");
                                #endregion

                                #region Socket Thread
                                Thread _thread = new Thread(
                                    () =>
                                    {
                                        try
                                        {
                                            #region Read Connect
                                            var _tcpWriter = new TcpClient();
                                            _tcpWriter.Connect(_ip, _port);
                                            _clientsServer.Add(_tcpWriter);

                                            var _tcp = _accept;
                                            int _maxErrorCount = 10;
                                            int _readBuffer = _tcp.ReceiveBufferSize;
                                            int _readBufferServer = _tcpWriter.ReceiveBufferSize;
                                            var _networkStream = new NetworkStream(_accept);
                                            var _networkStreamServer = _tcpWriter.GetStream();

                                            _clientsNS.Add(_networkStream);
                                            _clientsServerNS.Add(_networkStreamServer);

                                            var _buffer = new byte[_readBuffer];
                                            var _bufferServer = new byte[_readBufferServer];
                                            #endregion

                                            #region Read Thread
                                            Thread _readThread = new Thread(
                                                () =>
                                                {
                                                    int _errorCount = 0;
                                                    while (true)
                                                    {
                                                        try
                                                        {
                                                            var _len = _networkStream.Read(_buffer, 0, _buffer.Length);
                                                            if (_len > 0)
                                                            {
                                                                _networkStreamServer.Write(_buffer, 0, _len);

                                                                if (!_consoleDisable)
                                                                {
                                                                    string _result = System.Text.Encoding.UTF8.GetString(_buffer, 0, _len);
                                                                    writeError("CLIENT = ", ConsoleColor.DarkYellow);
                                                                    Console.WriteLine(_result);
                                                                }
                                                            }
                                                            else
                                                                Thread.Sleep(1000);
                                                        }
                                                        #region Error
                                                        catch
                                                        {
                                                            _errorCount++;
                                                            if (_errorCount > _maxErrorCount)
                                                            {
                                                                if (!_consoleDisable)
                                                                    writeError("Error max count error!");
                                                                break;
                                                            }
                                                        }
                                                        #endregion
                                                    }
                                                }
                                                );
                                            #endregion

                                            #region Write Thread
                                            Thread _writerThread = new Thread(
                                                () =>
                                                {
                                                    int _errorCount = 0;
                                                    while (true)
                                                    {
                                                        try
                                                        {
                                                            var _len = _networkStreamServer.Read(_bufferServer, 0, _bufferServer.Length);
                                                            if (_len > 0)
                                                            {
                                                                _networkStream.Write(_bufferServer, 0, _len);

                                                                if (!_consoleDisable)
                                                                {
                                                                    string _result = System.Text.Encoding.UTF8.GetString(_bufferServer, 0, _len);
                                                                    writeError("SERVER = ", ConsoleColor.DarkYellow);
                                                                    Console.WriteLine(_result);
                                                                }
                                                            }
                                                            else
                                                                Thread.Sleep(1000);
                                                        }
                                                        #region Error
                                                        catch
                                                        {
                                                            _errorCount++;
                                                            if (_errorCount > _maxErrorCount)
                                                            {
                                                                if (!_consoleDisable)
                                                                    writeError("Error max count error!");
                                                                break;
                                                            }
                                                        }
                                                        #endregion
                                                    }
                                                }
                                                );
                                            #endregion

                                            #region Start
                                            _writerThread.Start();
                                            _readThread.Start();
                                            _connectThreads.Add(_readThread);
                                            _connectThreads.Add(_writerThread);
                                            #endregion
                                        }
                                        #region Error
                                        catch
                                        {
                                            if (!_consoleDisable)
                                                writeError("Error tcp client...");
                                        }
                                        #endregion
                                    }
                                    );
                                #endregion

                                #region Start Socket
                                _thread.Start();
                                _connectThreads.Add(_thread);
                                #endregion
                            } while (true);
                        }
                        #region Error
                        catch
                        {
                            if (!_consoleDisable)
                                writeError("Server accept error!");
                        }
                        #endregion
                        _server.Stop();
                    }
                    );
                #endregion

                _waitThread.Start();

                #region Wait Codes
                do
                {
                    Console.WriteLine();
                    writeError("-------");
                    writeError("Write code(help : Active Codes) = ", ConsoleColor.Blue);
                    var _code = Console.ReadLine();
                    bool _notFoundCommand = false;

                    switch (_code.ToLower())
                    {
                        case "clear":
                            Console.Clear();
                            break;
                        #region connections
                        case "connections":
                            try
                            {
                                List<Socket> _socketsCopy = new List<Socket>();

                                Parallel.ForEach(_clients, (item) =>
                                {
                                    _socketsCopy.Add(item);
                                });
                                var _list = _socketsCopy.GroupBy(o => o.RemoteEndPoint.ToString()).Select(o => o.Key).ToList();
                                Parallel.ForEach(_list, (item) =>
                                {
                                    var _str = "IP = " + item;
                                    Console.WriteLine(_str);
                                });
                                writeError("Connection count = " + _list.Count);
                                _list.Clear();
                                _socketsCopy.Clear();
                            }
                            catch 
                            {
                                writeError("Thread cross error!");
                                _notFoundCommand = true;
                            }
                            break;
                        #endregion
                        #region abort
                        case "abort":
                            _consoleDisable = true;
                            _waitThread.Abort();
                            Parallel.ForEach(_connectThreads, (item) =>
                            {
                                try
                                {
                                    item.Abort();
                                }
                                catch
                                { }
                            });

                            _server.Stop();
                            Parallel.ForEach(_clients, (item) =>
                            {
                                try
                                {
                                    item.Dispose();
                                }
                                catch
                                { }
                            });
                            Parallel.ForEach(_clientsServer, (item) =>
                            {
                                try
                                {
                                    item.Close();
                                }
                                catch
                                { }
                            });
                            Parallel.ForEach(_clientsNS, (item) =>
                            {
                                try
                                {
                                    item.Dispose();
                                }
                                catch
                                { }
                            });
                            Parallel.ForEach(_clientsServerNS, (item) =>
                            {
                                try
                                {
                                    item.Dispose();
                                }
                                catch
                                { }
                            });
                            break;
                        #endregion
                        #region start_network, stop_network
                        case "start_network":
                            _consoleDisable = false;
                            break;
                        case "stop_network":
                            _consoleDisable = true;
                            break;
                        #endregion
                        #region help
                        case "help":
                            Console.WriteLine("clear = Console Clear");
                            Console.WriteLine("connections = List Active Connections");
                            Console.WriteLine("abort = Abort Server and All Connections");
                            Console.WriteLine("start_network = Start All Network Datas and Error Write Console");
                            Console.WriteLine("stop_network = Stop All Network Datas and Error Write Console");
                            break;
                        #endregion
                        default:
                            writeError("Not found command!");
                            _notFoundCommand = true;
                            break;
                    }
                    if (!_notFoundCommand)
                        writeError("Command success...", ConsoleColor.Blue);

                    #region abort break
                    if (_code == "abort")
                    {
                        clearSettings(out _port, out _ip, out _waitPort);
                        break;
                    }
                    #endregion
                } while (true);
                #endregion
            }
        }

        private static void clearSettings(out int _port, out string _ip, out int _waitPort)
        {
            _ip = "";
            _port = 0;
            _waitPort = 0;
        }

        static void writeError(string code, ConsoleColor color = ConsoleColor.Red)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(code);
            Console.ForegroundColor = ConsoleColor.Green;
        }

        private static int getNumber(string msg)
        {
            int _port;
            do
            {
                Console.Write("Please write " + msg + " = ");
                string _portStr = Console.ReadLine();
                if (!int.TryParse(_portStr, out _port))
                {
                    Console.WriteLine("Is not number!");
                    continue;
                }
                break;
            } while (true);
            return _port;
        }
    }
}
