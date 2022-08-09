using System.Text;
using System.Net.Sockets;
using System.Net;

class Program
{
    public static void Main(string[] args)
    {
        string ip;
        int packetSize;
        int timeout;
        int attempts;

        try
        {
            //ip = "8.8.8.8";
            //packetSize = 1400;
            //timeout = 25;
            //attempts = 4;

            ip = args[0];
            packetSize = int.Parse(args[1]);
            timeout = int.Parse(args[2]);
            attempts = int.Parse(args[3]);
        }
        catch (Exception ex) when (ex is ArgumentNullException || ex is OverflowException || ex is FormatException || ex is IndexOutOfRangeException)
        {
            Console.WriteLine("Invalid command line arguments\n" +
                                "\tMyPing [IP] [SIZE] [TIMEOUT] [ATTEMPTS]");
            return;
        }

        Pinger.Ping(ip, packetSize, timeout, attempts);
    }
}
class Pinger
{
    public static void Ping(string ip, int packetSize = 128, int timeout = 500, int attempts = 4)
    {
        int lostPackets = 0;
        int deliveredPackets = 0;
        int currentPing = 0;
        int minPing = int.MaxValue;
        int maxPing = 0;
        int[] pingValues = new int[0];
        Icmp icmp = new Icmp();
        Socket host = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);

        try
        {

            icmp.setSize(packetSize);
            pingValues = new int[attempts];
            host.Bind(new IPEndPoint(IPAddress.Parse("192.168.28.110"), 0));
            host.Connect(IPAddress.Parse(ip), 0);
            host.ReceiveTimeout = timeout;
        }

        catch (Exception ex) when (ex is SocketException)
        {
            Console.WriteLine("Connection error\n");
            return;
        }

        byte[] id = new byte[4];
        byte[] TTL = new byte[1];
        byte[] buffer = new byte[65536];
        byte[] sourceAddress = new byte[4];
        byte[] destinationAddress = new byte[4];

        int recievedBytes = 0;

        for (int i = 0; i < attempts; i++)
        {

            Buffer.BlockCopy(BitConverter.GetBytes(i), 0, icmp.message, 0, 4);
            icmp.checkSum = icmp.getChecksum();

            try
            {
                host.Send(icmp.getBytes());

                DateTime sentTime = DateTime.Now;
                recievedBytes = host.Receive(buffer);
                deliveredPackets++;
                DateTime recievedTime = DateTime.Now;

                currentPing = (int)(recievedTime - sentTime).TotalMilliseconds;

                Buffer.BlockCopy(buffer, 8, TTL, 0, 1);
                Buffer.BlockCopy(buffer, 12, destinationAddress, 0, 4);
                Buffer.BlockCopy(buffer, 16, sourceAddress, 0, 4);
                Buffer.BlockCopy(buffer, 24, id, 0, 4);
                Buffer.BlockCopy(buffer, 24, buffer, 0, recievedBytes);

                if (BitConverter.ToInt32(id) != i) throw new SocketException();

                Console.WriteLine($"Response from {string.Join(".", destinationAddress)}: size = {packetSize}, ping = {currentPing} ms, TTL = {TTL[0]}");

                minPing = Math.Min(minPing, currentPing);
                maxPing = Math.Max(maxPing, currentPing);
                pingValues[i] = currentPing;
            }
            catch (Exception ex) when (ex is SocketException)
            {
                Console.WriteLine("Response timeout");
                lostPackets++;
            }

            Thread.Sleep(500);
        }


        Console.WriteLine($"\nStatistic for {ip}: \n" +
                            $"\tPackets: sent = {attempts}, delivered = {deliveredPackets}, lost = {lostPackets} ({(double)lostPackets / attempts * 100}% loss)\n" +
                            $"Approximate round-trip time in ms:\n" +
                            $"\tMinimum = {minPing} ms, Maximum = {maxPing} ms, Average = {pingValues.Where(a => a != 0).Sum() / attempts} ms\n");
    }

    struct Icmp
    {
        public byte type;
        public byte code;
        public Int16 checkSum;
        private int messageSize;
        public int MessageSize
        {
            get { return messageSize; }
            set { messageSize = value; message = new byte[value]; }
        }
        public byte[] message;

        public Icmp()
        {
            type = 0x08;
            code = 0x00;
            checkSum = 0x00;
            messageSize = 0;
            message = new byte[0];
        }

        public Icmp(string data)
        {
            type = 0x08;
            code = 0x00;
            checkSum = 0x00;
            message = Encoding.UTF8.GetBytes(data);
            messageSize = Encoding.UTF8.GetBytes(data).Length;
        }

        public Icmp(byte[] data, int size)
        {
            type = data[20];
            code = data[21];
            checkSum = BitConverter.ToInt16(data, 22);
            messageSize = size - 24;
            message = new byte[messageSize];
            Buffer.BlockCopy(data, 24, message, 0, messageSize);
        }

        public byte[] getBytes()
        {
            byte[] data = new byte[messageSize + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(type), 0, data, 0, 1);
            Buffer.BlockCopy(BitConverter.GetBytes(code), 0, data, 1, 1);
            Buffer.BlockCopy(BitConverter.GetBytes(checkSum), 0, data, 2, 2);
            Buffer.BlockCopy(message, 0, data, 4, messageSize);
            return data;
        }

        public Int16 getChecksum()
        {
            checkSum = 0;
            UInt32 chcksm = 0;
            byte[] data = getBytes();
            int packetsize = MessageSize + 4;
            int index = 0;

            while (index < packetsize)
            {
                chcksm += Convert.ToUInt32(BitConverter.ToUInt16(data, index));
                index += 2;
            }
            chcksm = (chcksm >> 16) + (chcksm & 0xffff);
            chcksm += (chcksm >> 16);
            return (Int16)(~chcksm);
        }
        public void setSize(int size)
        {
            message = new byte[size - 4];
            for (int i = 0; i < size - 4; i++)
                message[i] = (byte)(10);

            messageSize = size - 4;
        }
    }
}

