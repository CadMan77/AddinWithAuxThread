﻿Imports System.IO

Module GlobalDate
	Sub AppendLineToLogFile(line As String)
		SyncLock _logFileLockObject
			AppendLineToFile(logPath, line)
		End SyncLock
	End Sub
	Sub AppendLineToFile(filePath As String, line As String)
		Using writer As New StreamWriter(filePath, True) ' open for append
			writer.WriteLine(line)
		End Using
	End Sub

	'Function GetGlobalDateTimeFromWinSrv() As DateTime
	'Async Function GetGlobalDateTimeFromWinSrv() As Task(Of DateTime)
	'	Dim utcNtpDT As DateTime = NtpClient.GetNetworkTime()

	'	If utcNtpDT = DateTime.MinValue Then
	'		AppendLineToLogFile("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")
	'		Return DateTime.MinValue
	'	End If

	'	Return utcNtpDT
	'End Function

	Async Function GetGlobalDateTimeFromWinSrv() As Task(Of DateTime)
		Return Await Task.Run(Function()
								  Dim utcNtpDT As DateTime = NtpClient.GetNetworkTime()

								  If utcNtpDT = DateTime.MinValue Then
									  AppendLineToLogFile("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")
									  Return DateTime.MinValue
								  End If

								  Return utcNtpDT
							  End Function)
	End Function


End Module
