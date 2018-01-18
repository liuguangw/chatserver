using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace chatserver
{
    class Program
    {
        private List<WebSocket> webSocketList;
        private HttpListener listener;
        static void Main(string[] args)
        {
            (new Program()).startServer(8085, "127.0.0.1");
        }

        private void startServer(int port, string host)
        {
            string url = "http://" + host + ":" + port + "/";
            this.listener = new HttpListener();
            this.listener.Prefixes.Add(url);
            try
            {

                this.listener.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            Console.WriteLine("server started at " + url + "...");
            this.webSocketList = new List<WebSocket>();
            Task.Run(new Action(clientAccepter));
            Console.ReadLine();
        }

        private async void clientAccepter()
        {
            while (true)
            {
                HttpListenerContext context = await this.listener.GetContextAsync();
                this.processClient(context);
            }
        }

        private Task processClient(HttpListenerContext context)
        {
            return Task.Run(() =>
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                if (request.IsWebSocketRequest)
                {
                    this.processWebSocketRequest(context);
                }
                else
                {
                    this.processHttpRequest(request, response);
                }
            });
        }

        private Task processWebSocketRequest(HttpListenerContext context)
        {
            return Task.Run(async () =>
            {
                try
                {
                    HttpListenerWebSocketContext httpListenerWebSocket = await context.AcceptWebSocketAsync(null);

                    lock (this.webSocketList)
                    {
                        this.webSocketList.Add(httpListenerWebSocket.WebSocket);
                    }
                    IPEndPoint endPoint = context.Request.RemoteEndPoint;
                    string tip = "websocket connect from " + endPoint.Address + ":" + endPoint.Port + "(当前在线人数:" + this.webSocketList.Count + ")";
                    Console.WriteLine(tip);
                    this.broadcastMessage(tip);
                    this.acceptClientSock(httpListenerWebSocket.WebSocket);
                }
                catch (Exception e1)
                {
                    Console.WriteLine(e1.Message);
                }
            });
        }

        private Task acceptClientSock(WebSocket webSocket)
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    ArraySegment<byte> buff = new ArraySegment<byte>(new byte[2048]);
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(buff, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            lock (this.webSocketList)
                            {
                                this.webSocketList.Remove(webSocket);
                            }
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        UTF8Encoding encoding = new UTF8Encoding();
                        string text = "[收到消息]:" + encoding.GetString(buff.Array).TrimEnd('\0');
                        Console.WriteLine(text);
                        this.broadcastMessage(text);
                    }
                }
            });
        }

        private Task processHttpRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            return Task.Run(() =>
            {
                try
                {
                    Console.WriteLine(request.HttpMethod + " " + request.Url.AbsoluteUri);
                    UTF8Encoding encoding = new UTF8Encoding();
                    response.ContentType = "text/plain; charset=utf-8";
                    byte[] msg = encoding.GetBytes("你好,世界!");
                    response.OutputStream.Write(msg, 0, msg.Length);
                    response.Close();
                }
                catch (Exception e1)
                {
                    Console.WriteLine(e1.Message);
                }
            });
        }

        private void broadcastMessage(string text)
        {

            foreach (WebSocket client in this.webSocketList)
            {
                UTF8Encoding encoding = new UTF8Encoding();
                client.SendAsync(new ArraySegment<byte>(encoding.GetBytes(text)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
