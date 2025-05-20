Imports System.Net
Imports System.Net.Sockets

Public Class NtpClient

    ''' <summary>
    ''' Gets the current DateTime from time.windows.com.
    ''' </summary>
    ''' <returns>A DateTime containing the current time.</returns>
    Public Shared Function GetNetworkTime() As DateTime
        Return GetNetworkTime("time.windows.com") ' You can replace with another NTP server
    End Function

    ''' <summary>
    ''' Gets the current DateTime from the specified NTP server.
    ''' </summary>
    ''' <param name="ntpServer">The hostname of the NTP server.</param>
    ''' <returns>A DateTime containing the current time.</returns>
    Public Shared Function GetNetworkTime(ntpServer As String) As DateTime
        Dim address As IPAddress()

        Try
            address = Dns.GetHostAddresses(ntpServer)
        Catch ' (No Internet) Exception
            Return DateTime.MinValue
        End Try

        If address Is Nothing OrElse address.Length = 0 Then
            Throw New ArgumentException("Could not resolve IP address from '" & ntpServer & "'.", "ntpServer")
        End If

        Dim ep As New IPEndPoint(address(0), 123)
        Return GetNetworkTime(ep)
    End Function


    ''' <summary>
    ''' Gets the current DateTime from the specified IPEndPoint.
    ''' </summary>
    ''' <param name="ep">The IPEndPoint to connect to.</param>
    ''' <returns>A DateTime containing the current time.</returns>
    Public Shared Function GetNetworkTime(ep As IPEndPoint) As DateTime
        Dim s As New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        s.Connect(ep)

        Dim ntpData As Byte() = New Byte(47) {}
        ntpData(0) = &H1B ' NTP request header

        s.Send(ntpData)
        s.Receive(ntpData)
        s.Close()

        ' Extract timestamp
        Dim offsetTransmitTime As Byte = 40
        Dim intpart As ULong = 0
        Dim fractpart As ULong = 0

        For i As Integer = 0 To 3
            intpart = 256 * intpart + ntpData(offsetTransmitTime + i)
        Next

        For i As Integer = 4 To 7
            fractpart = 256 * fractpart + ntpData(offsetTransmitTime + i)
        Next

        Dim milliseconds As ULong = (intpart * 1000UL + (fractpart * 1000UL) / &H100000000L)

        Dim utcDT As New DateTime(1900, 1, 1)
        utcDT = utcDT.AddMilliseconds(milliseconds)

        Return utcDT
    End Function
End Class
