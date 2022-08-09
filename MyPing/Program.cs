using System.Text;
using System.Net.Sockets;
using System.Collections;

string ip;
int packetSize;
Icmp icmp = new Icmp();
Socket host = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);


try
{
    ip = "8.8.8.8";
    packetSize = 1024;
    //ip = args[0];
    //packetSize = int.Parse(args[1]);
    icmp.messageSize = packetSize;
    host.Connect(System.Net.IPAddress.Parse(ip), 0);
}
catch(Exception ex) when (ex is ArgumentNullException || ex is OverflowException || ex is FormatException)
{
    Console.WriteLine("Invalid command line arguments\n" +
                        "\tMyPing [IP] [PING]");
    return -1; 
}
catch(Exception ex) when (ex is SocketException)
{
    Console.WriteLine("Connection error\n");
    return -1;
}


while (true)
{
    byte[] TTL = new byte[1]; 
    byte[] buffer = new byte[packetSize];
    byte[] sourceAddress = new byte[4];
    byte[] destinationAddress = new byte[4];

    icmp.checkSum = icmp.getChecksum();
    host.Send(icmp.getBytes());

    DateTime sentTime = DateTime.Now;
    var recievedBytes = host.Receive(buffer);
    DateTime recievedTime = DateTime.Now;

    Buffer.BlockCopy(buffer, 8, TTL, 0, 1);
    Buffer.BlockCopy(buffer, 12, destinationAddress, 0, 4);
    Buffer.BlockCopy(buffer, 16, sourceAddress, 0, 4);
    Buffer.BlockCopy(buffer, 24, buffer, 0, recievedBytes);

    Console.WriteLine($"Response from {string.Join(".", destinationAddress)}: size = {packetSize}, ping = {(int)(recievedTime - sentTime).TotalMilliseconds} ms, TTL = {TTL[0]}");
    Thread.Sleep(500);
}


struct Icmp
{
    public byte type;
    public byte code;
    public Int16 checkSum;
    public int messageSize;
    public byte[] message = new byte[1024];

    public Icmp()
    {
        type = 0x08;
        code = 0x00;
        checkSum = 0x00;
        messageSize = 0;
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
        Buffer.BlockCopy(data, 24, message, 0, messageSize);
    }

    public byte[] getBytes()
    {
        byte[] data = new byte[messageSize + 9];
        Buffer.BlockCopy(BitConverter.GetBytes(type), 0, data, 0, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(code), 0, data, 1, 1);
        Buffer.BlockCopy(BitConverter.GetBytes(checkSum), 0, data, 2, 2);
        Buffer.BlockCopy(message, 0, data, 4, messageSize);
        return data;
    }

    public Int16 getChecksum()
    {
        checkSum = 0;
        UInt32 checksum = 0;
        byte[] data = getBytes();
        int currentBytes = 0;

        while (currentBytes < messageSize + 8)
        {
            checksum += Convert.ToUInt32(BitConverter.ToUInt16(data, currentBytes));
            currentBytes += 2;
        }

        checksum = (checksum >> 16) + (checksum);
        return (Int16)(~checksum);
    }

    public void setSize(int size)
    {
        for(int i = 0; i < size - 4; i++)
        {
            message[i] = (byte)(i % 255);
        }
        messageSize = size;
    }
}