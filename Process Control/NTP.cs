using System;
using System.Net;
using System.Net.Sockets;

public static class Ntp
{
    public static bool UpdateTimeFromNtpServer(string server, int timeZoneOffset)
    {
        try
        {
            var currentTime = GetNtpTime(server, timeZoneOffset);
            Microsoft.SPOT.Hardware.Utility.SetLocalTime(currentTime);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get DateTime from NTP Server
    /// Based on:
    /// http://weblogs.asp.net/mschwarz/archive/2008/03/09/wrong-datetime-on-net-micro-framework-devices.aspx
    /// </summary>
    /// <param name="timeServer">Time Server (NTP) address</param>
    /// <param name="timeZoneOffset">Difference in hours from UTC</param>
    /// <returns>Local NTP Time</returns>
    private static DateTime GetNtpTime(String timeServer, int timeZoneOffset)
    {
        // Find endpoint for TimeServer
        var ep = new IPEndPoint(Dns.GetHostEntry(timeServer).AddressList[0], 123);

        // Make send/receive buffer
        var ntpData = new byte[48];

        // Connect to TimeServer
        using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            // Set 10s send/receive timeout and connect
            s.SendTimeout = s.ReceiveTimeout = 2500; // 2,500 ms
            s.Connect(ep);

            // Set protocol version
            ntpData[0] = 0x1B;

            // Send Request
            s.Send(ntpData);

            // Receive Time
            s.Receive(ntpData);

            // Close the socket
            s.Close();
        }

        const byte offsetTransmitTime = 40;

        ulong intpart = 0;
        ulong fractpart = 0;

        for (var i = 0; i <= 3; i++)
            intpart = (intpart << 8) | ntpData[offsetTransmitTime + i];

        for (var i = 4; i <= 7; i++)
            fractpart = (fractpart << 8) | ntpData[offsetTransmitTime + i];

        ulong milliseconds = (intpart * 1000 + (fractpart * 1000) / 0x100000000L);

        var timeSpan = TimeSpan.FromTicks((long)milliseconds * TimeSpan.TicksPerMillisecond);
        var dateTime = new DateTime(1900, 1, 1);
        dateTime += timeSpan;

        var offsetAmount = new TimeSpan(timeZoneOffset, 0, 0);
        var networkDateTime = (dateTime + offsetAmount);

        return networkDateTime;
    }
}