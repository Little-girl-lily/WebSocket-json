using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Data.SqlClient;
using System.IO;
using Newtonsoft.Json;
using System.Data;


namespace WebSocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 1817;
            byte[] buffer = new byte[1024];

            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, port);
            Socket listener = new Socket(localEP.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                listener.Bind(localEP);
                listener.Listen(10);

                Console.WriteLine("等待客户端连接....");
                Socket sc = listener.Accept();//接受一个连接
                Console.WriteLine("接受到了客户端：" + sc.RemoteEndPoint.ToString() + "连接....");

                //握手
                int length = sc.Receive(buffer);//接受客户端握手信息
                sc.Send(PackHandShakeData(GetSecKeyAccetp(buffer, length)));
                Console.WriteLine("已经发送握手协议了....");

                //接受客户端数据
                Console.WriteLine("等待客户端数据....");

               
                while (true)
                { length = sc.Receive(buffer);//接受客户端信息
                string clientMsg = AnalyticData(buffer, length);
                if(clientMsg == "")
                { 
                    sc.Close();//关闭连接
                 }
               // else{
                Console.WriteLine("接受到客户端数据：" + clientMsg);
                // 获取sockClient1对应的内核接收缓冲区大小  
                //int SendSize = sc.SendBufferSize;
                while(true){ GetJsonFile(sc); }
                //定时获得数据库中数据传给前台
                //System.Timers.Timer t = new System.Timers.Timer(1000);   //实例化Timer类，设置间隔时间为10000毫秒；   
                //t.Elapsed += delegate { GetJsonFile(sc); };//到达时间的时候执行事件；
                //t.AutoReset = true;   //设置是执行一次（false）还是一直执行(true)；   
                //t.Enabled = true;     //是否执行System.Timers.Timer.Elapsed事件；   

                //System.Threading.Thread.Sleep(1000);
               }
            }
            //}
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        ///<summary>
        ///获得数据库中数据传给前台
        ///</summary>
        private static void GetJsonFile(Socket sc)
        {
            string sms = DateTime.Now.ToString("mm:ss.ms");
            string path = "json/93_22.txt";
            string fileName = Path.GetFileNameWithoutExtension(path);//获取文件名,不带后缀
            string[] fileNameA = fileName.Split('_');
            DataTable ValueDt =  getValue(fileNameA); //搜数据库，获取'stateValue'的值

            //发送数据statuValue
            string sendMsg="";
            for(int j=0;j<ValueDt.Rows.Count;j++){
                sendMsg += ValueDt.Rows[j]["RtuNo"].ToString().Replace(" ", "") + ValueDt.Rows[j]["OrderNo"].ToString().Replace(" ", "") + "-" + ValueDt.Rows[j]["LastValue"].ToString().Replace(" ", "");
              if (j < ValueDt.Rows.Count-1) {
                  sendMsg += "_";
              }
            }

            string ems = DateTime.Now.ToString("mm:ss.ms");
            Console.WriteLine(sms + " end:" + ems + " 发送数据：“" + sendMsg + "” 至客户端....");
          
            //string S2 = sendMsg.Substring(0, 126);//截取字符串substring
            sc.Send(PackData(sendMsg));
        }
        ///<summary
        ///搜数据库，获取'stateValue'的值
        ///<summary 
        private static DataTable getValue(string[] fileNameA)
        {
            //使用windows验证，采用SSPI
            string str = @"Server=QT-201704101033\SQLEXPRESS;database=WebSocketTestDB;integrated security=SSPI";
            SqlConnection conn = new SqlConnection(str);
            conn.Open();
            string selectsql = "select * from WebPicsPoint where RtuNo ='" + fileNameA[0] + "' and PicNo='" + fileNameA[1] + "'";
            SqlCommand cmd = new SqlCommand(selectsql, conn);//SqlCommand对象允许你指定在数据库上执行的操作的类型。  
            DataTable dt = new DataTable(); 
            DataTable ValueDt = new DataTable(); 
            SqlDataAdapter adp = new SqlDataAdapter(cmd); 
            adp.Fill(dt);
            string AiOrderNo="";
            string DiOrderNo = "";
            for (int i = 0; i < dt.Rows.Count; i++) {
                string DataType = dt.Rows[i]["DataType"].ToString().Replace(" ", "");
                if (DataType == "AI")
                { 
                    AiOrderNo += ",";
                    AiOrderNo += dt.Rows[i]["OrderNo"].ToString().Replace(" ","");
                }
                if (DataType == "DI")
                { 
                    DiOrderNo += ",";
                    DiOrderNo += dt.Rows[i]["OrderNo"].ToString().Replace(" ", "");
                }
            }

            DiOrderNo=DiOrderNo.Substring(1);//删除开始的逗号，使用substring会赋值给一个新的数组
            AiOrderNo=AiOrderNo.Substring(1);

            string sql1 = "select RtuNo,OrderNo,LastValue from Ai where RtuNo = " + fileNameA[0] + " and OrderNo in(" + AiOrderNo + ")";
            cmd = new SqlCommand(sql1, conn);//SqlCommand对象允许你指定在数据库上执行的操作的类型。  
            adp = new SqlDataAdapter(cmd);
            adp.Fill(ValueDt);

            string sql2 = "select RtuNo,OrderNo,LastValue from Di where RtuNo = " + fileNameA[0] + " and OrderNo in(" + DiOrderNo + ")";
            cmd = new SqlCommand(sql2, conn);//SqlCommand对象允许你指定在数据库上执行的操作的类型。  
            adp = new SqlDataAdapter(cmd);
            adp.Fill(ValueDt);
 
            conn.Close();
            return ValueDt;
        }
        /// <summary>
        /// 打包握手信息
        /// </summary>
        /// <param name="secKeyAccept">Sec-WebSocket-Accept</param>
        /// <returns>数据包</returns>
        private static byte[] PackHandShakeData(string secKeyAccept)
        {
            var responseBuilder = new StringBuilder();
            responseBuilder.Append("HTTP/1.1 101 Switching Protocols" + Environment.NewLine);
            responseBuilder.Append("Upgrade: websocket" + Environment.NewLine);
            responseBuilder.Append("Connection: Upgrade" + Environment.NewLine);
            responseBuilder.Append("Sec-WebSocket-Accept: " + secKeyAccept + Environment.NewLine + Environment.NewLine);
            //如果把上一行换成下面两行，才是thewebsocketprotocol-17协议，但居然握手不成功，目前仍没弄明白！
            //responseBuilder.Append("Sec-WebSocket-Accept: " + secKeyAccept + Environment.NewLine);
            //responseBuilder.Append("Sec-WebSocket-Protocol: chat" + Environment.NewLine);

            return Encoding.UTF8.GetBytes(responseBuilder.ToString());
        }

        /// <summary>
        /// 生成Sec-WebSocket-Accept
        /// </summary>
        /// <param name="handShakeText">客户端握手信息</param>
        /// <returns>Sec-WebSocket-Accept</returns>
        private static string GetSecKeyAccetp(byte[] handShakeBytes, int bytesLength)
        {
            string handShakeText = Encoding.UTF8.GetString(handShakeBytes, 0, bytesLength);
            string key = string.Empty;
            Regex r = new Regex(@"Sec\-WebSocket\-Key:(.*?)\r\n");
            Match m = r.Match(handShakeText);
            if (m.Groups.Count != 0)
            {
                key = Regex.Replace(m.Value, @"Sec\-WebSocket\-Key:(.*?)\r\n", "$1").Trim();
            }
            byte[] encryptionString = SHA1.Create().ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
            return Convert.ToBase64String(encryptionString);
        }

        /// <summary>
        /// 解析客户端数据包
        /// </summary>
        /// <param name="recBytes">服务器接收的数据包</param>
        /// <param name="recByteLength">有效数据长度</param>
        /// <returns></returns>
        private static string AnalyticData(byte[] recBytes, int recByteLength)
        {
            if (recByteLength < 2) { return string.Empty; }

            bool fin = (recBytes[0] & 0x80) == 0x80; // 1bit，1表示最后一帧  
            if (!fin)
            {
                return string.Empty;// 超过一帧暂不处理 
            }

            bool mask_flag = (recBytes[1] & 0x80) == 0x80; // 是否包含掩码  
            if (!mask_flag)
            {
                return string.Empty;// 不包含掩码的暂不处理
            }

            int payload_len = recBytes[1] & 0x7F; // 数据长度  

            byte[] masks = new byte[4];
            byte[] payload_data;

            if (payload_len == 126)
            {
                Array.Copy(recBytes, 4, masks, 0, 4);
                payload_len = (UInt16)(recBytes[2] << 8 | recBytes[3]);
                payload_data = new byte[payload_len];
                Array.Copy(recBytes, 8, payload_data, 0, payload_len);

            }
            else if (payload_len == 127)
            {
                Array.Copy(recBytes, 10, masks, 0, 4);
                byte[] uInt64Bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    uInt64Bytes[i] = recBytes[9 - i];
                }
                UInt64 len = BitConverter.ToUInt64(uInt64Bytes, 0);

                payload_data = new byte[len];
                for (UInt64 i = 0; i < len; i++)
                {
                    payload_data[i] = recBytes[i + 14];
                }
            }
            else
            {
                Array.Copy(recBytes, 2, masks, 0, 4);
                payload_data = new byte[payload_len];
                Array.Copy(recBytes, 6, payload_data, 0, payload_len);

            }

            for (var i = 0; i < payload_len; i++)
            {
                payload_data[i] = (byte)(payload_data[i] ^ masks[i % 4]);
            }

            return Encoding.UTF8.GetString(payload_data);
        }


        /// <summary>
        /// 打包服务器数据
        /// </summary>
        /// <param name="message">数据</param>
        /// <returns>数据包</returns>
        private static byte[] PackData(string message)
        {
            byte[] contentBytes = null;
            byte[] temp = Encoding.UTF8.GetBytes(message);
            //byte[] temp = Encoding.Default.GetBytes(message);

            if (temp.Length < 126)
            {
                contentBytes = new byte[temp.Length + 2];
                contentBytes[0] = 0x81;
                contentBytes[1] = (byte)temp.Length;
                Array.Copy(temp, 0, contentBytes, 2, temp.Length);
            }
            else if (temp.Length < 0xFFFF)//65535
            {
                contentBytes = new byte[temp.Length + 4];
                contentBytes[0] = 0x81;
                contentBytes[1] = 126;
                contentBytes[2] = (byte)(temp.Length >> 8 & 0xFF);//跟下面一行交换了(byte)(temp.Length & 0xFF);
                contentBytes[3] = (byte)(temp.Length & 0xFF);
                Array.Copy(temp, 0, contentBytes, 4, temp.Length);
            }
            else
            {
                // 暂不处理超长内容  
            }

            return contentBytes;
        }
       
    }
}