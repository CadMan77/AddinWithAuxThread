﻿Imports Inventor
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports System.Resources
Imports System.Threading

Friend Module Globals
    Friend dllName As String = My.Application.Info.AssemblyName

    Friend invApp As Inventor.Application ' Inventor application object.

    Friend globDateTime As DateTime
    Friend GetGlobDateTimeTask As Task
    Friend cts As CancellationTokenSource
    Friend _logFileLockObject As New Object()
    Friend logPath As String = "C:\Temp\AuxThread-(DateTime).LOG"


    ' This function uses reflection to get the GuidAttribute associated with the add-in.
    Public Function AddInClientID() As String
        Dim guid As String = ""
        Try
            Dim t As Type = GetType(AddinWithAuxThread.StandardAddInServer)
            Dim customAttributes() As Object = t.GetCustomAttributes(GetType(GuidAttribute), False)
            Dim guidAttribute As GuidAttribute = CType(customAttributes(0), GuidAttribute)
            guid = guidAttribute.Value.ToString()
        Catch
        End Try

        Return guid
    End Function

#Region "hWnd Wrapper Class"
    ' This class is used to wrap a Win32 hWnd as a .Net IWind32Window class.
    ' This is primarily used for parenting a dialog to the Inventor window.
    '
    ' For example:
    ' myForm.Show(New WindowWrapper(g_inventorApplication.MainFrameHWND))
    '
    Public Class WindowWrapper
        Implements System.Windows.Forms.IWin32Window
        Public Sub New(ByVal handle As IntPtr)
            _hwnd = handle
        End Sub

        Public ReadOnly Property Handle() As IntPtr _
          Implements System.Windows.Forms.IWin32Window.Handle
            Get
                Return _hwnd
            End Get
        End Property

        Private _hwnd As IntPtr
    End Class
#End Region

#Region "Image Converter"
    ' Class used to convert bitmaps and icons from their .Net native types into
    ' an IPictureDisp object which is what the Inventor API requires. A typical
    ' usage is shown below where MyIcon is a bitmap OrElse icon that's available
    ' as a resource of the project.
    '
    ' Dim smallIcon As stdole.IPictureDisp = PictureDispConverter.ToIPictureDisp(My.Resources.MyIcon)

    Public NotInheritable Class PictureDispConverter
        <DllImport("OleAut32.dll", EntryPoint:="OleCreatePictureIndirect", ExactSpelling:=True, PreserveSig:=False)>
        Private Shared Function OleCreatePictureIndirect(
            <MarshalAs(UnmanagedType.AsAny)> ByVal picdesc As Object,
            ByRef iid As Guid,
            <MarshalAs(UnmanagedType.Bool)> ByVal fOwn As Boolean) As stdole.IPictureDisp
        End Function

        Shared iPictureDispGuid As Guid = GetType(stdole.IPictureDisp).GUID

        Private NotInheritable Class PICTDESC
            Private Sub New()
            End Sub

            'Picture Types
            Public Const PICTYPE_BITMAP As Short = 1
            Public Const PICTYPE_ICON As Short = 3

            <StructLayout(LayoutKind.Sequential)>
            Public Class Icon
                Friend cbSizeOfStruct As Integer = Marshal.SizeOf(GetType(PICTDESC.Icon))
                Friend picType As Integer = PICTDESC.PICTYPE_ICON
                Friend hicon As IntPtr = IntPtr.Zero
                Friend unused1 As Integer
                Friend unused2 As Integer

                Friend Sub New(ByVal icon As System.Drawing.Icon)
                    Me.hicon = icon.ToBitmap().GetHicon()
                End Sub
            End Class

            <StructLayout(LayoutKind.Sequential)>
            Public Class Bitmap
                Friend cbSizeOfStruct As Integer = Marshal.SizeOf(GetType(PICTDESC.Bitmap))
                Friend picType As Integer = PICTDESC.PICTYPE_BITMAP
                Friend hbitmap As IntPtr = IntPtr.Zero
                Friend hpal As IntPtr = IntPtr.Zero
                Friend unused As Integer

                Friend Sub New(ByVal bitmap As System.Drawing.Bitmap)
                    Me.hbitmap = bitmap.GetHbitmap()
                End Sub
            End Class
        End Class

        Public Shared Function ToIPictureDisp(ByVal icon As System.Drawing.Icon) As stdole.IPictureDisp
            Dim pictIcon As New PICTDESC.Icon(icon)
            Return OleCreatePictureIndirect(pictIcon, iPictureDispGuid, True)
        End Function

        Public Shared Function ToIPictureDisp(ByVal bmp As System.Drawing.Bitmap) As stdole.IPictureDisp
            Dim pictBmp As New PICTDESC.Bitmap(bmp)
            Return OleCreatePictureIndirect(pictBmp, iPictureDispGuid, True)
        End Function
    End Class
#End Region

End Module

Namespace AddinWithAuxThread
    <ProgIdAttribute("AddinWithAuxThread.StandardAddInServer"),
    GuidAttribute("8551D99F-F45E-4E0F-A033-736DF2060647")>
    Public Class StandardAddInServer
        Implements Inventor.ApplicationAddInServer

        Private WithEvents AppEvents As ApplicationEvents

        Private WithEvents UiEvents As UserInterfaceEvents


        ' This method is called by Inventor when it loads the AddIn. The AddInSiteObject provides access  
        ' to the Inventor Application object. The FirstTime flag indicates if the AddIn is loaded for
        ' the first time. However, with the introduction of the ribbon this argument is ALWAYS true.
        Public Async Sub Activate(ByVal addInSiteObject As ApplicationAddInSite, ByVal firstTime As Boolean) Implements ApplicationAddInServer.Activate
            ' Initialize AddIn members.

            Try

                '' Create an Auxiliary thread to get Global DateTime in Background

                cts = New CancellationTokenSource

                GetGlobDateTimeTask = Task.Run(Async Function()
                                                   AppendLineToLogFile("GlobalDT START")

                                                   Try
                                                       While Not cts.Token.IsCancellationRequested
                                                           globDateTime = DateTime.MinValue

                                                           globDateTime = Await GetGlobalDateTimeFromWinSrv()

                                                           AppendLineToLogFile(vbTab & TimeString & " Global DT is: " & globDateTime.ToString)

                                                           '' 1min GlobalDT request period
                                                           ' Thread.Sleep(60000) 
                                                           Await Task.Delay(60000, cts.Token)
                                                       End While

                                                       'Catch ex As Exception
                                                       '    AppendLineToLogFile(vbTab & "GlobalDT !!! EXCEPTION !!! " & ex.Message)

                                                   Catch
                                                       AppendLineToLogFile(vbTab & "GlobalDT !!! EXCEPTION !!!")
                                                       'Throw
                                                   End Try

                                                   AppendLineToLogFile("GlobalDT END")
                                               End Function, cts.Token)



                ' Call the Async method from another Async scope
                Await RunGlobalDateTimeTask()



                '' Track the Aux thread process (monitor the log with Notepad Plus Plus)
                If IO.File.Exists(logPath) Then IO.File.Delete(logPath)

                Dim nppPath = "C:\Program Files\Notepad++\notepad++.exe"
                Dim nppProcess As New Process()
                With nppProcess
                    .StartInfo.FileName = nppPath
                    .StartInfo.Arguments = logPath
                    .StartInfo.Arguments = "-monitor " & Chr(34) & logPath & Chr(34) ' Enables monitoring mode
                    .StartInfo.UseShellExecute = False
                    .StartInfo.RedirectStandardOutput = True
                End With
                nppProcess.Start()

                Thread.Sleep(2000)


                '' Await the task to properly catch AggregateException
                'Try
                '    Await GetGlobDateTimeTask
                'Catch ae As AggregateException
                '    AppendLineToLogFile(vbTab & "GlobalDT !!! AGGREGATE EXCEPTION !!! ")
                '    For Each ex In ae.InnerExceptions
                '        AppendLineToLogFile(vbTab & " - Exception: " & ex.Message)
                '    Next
                'Catch ex As Exception
                '    AppendLineToLogFile(vbTab & "GlobalDT !!! GENERAL EXCEPTION !!! " & ex.Message)
                'End Try

                'invApp = addInSiteObject.Application

                'AppEvents = invApp.ApplicationEvents()
                'UiEvents = invApp.UserInterfaceManager.UserInterfaceEvents()

                ' Add to the user interface, if it's the first time.
                ' TODO: Add button definitions.

                ' Sample to illustrate creating a button definition.
                'Dim largeIcon As stdole.IPictureDisp = PictureDispConverter.ToIPictureDisp(My.Resources.YourBigImage)
                'Dim smallIcon As stdole.IPictureDisp = PictureDispConverter.ToIPictureDisp(My.Resources.YourSmallImage)
                'Dim controlDefs As Inventor.ControlDefinitions = g_inventorApplication.CommandManager.ControlDefinitions
                'm_sampleButton = controlDefs.AddButtonDefinition("Command Name", "Internal Name", CommandTypesEnum.kShapeEditCmdType, AddInClientID)

                'End If

            Catch ex As Exception
                MessageBox.Show(ex.Message, dllName + " - " + MethodBase.GetCurrentMethod().Name, 0, MessageBoxIcon.Error)
            End Try
        End Sub



        Async Function RunGlobalDateTimeTask() As Task
            Try
                Await GetGlobDateTimeTask ' Now works inside an Async method
            Catch ae As AggregateException
                AppendLineToLogFile(vbTab & "GlobalDT !!! AGGREGATE EXCEPTION !!! ")
                For Each ex In ae.InnerExceptions
                    AppendLineToLogFile(vbTab & " - Exception: " & ex.Message)
                Next
            Catch ex As Exception
                AppendLineToLogFile(vbTab & "GlobalDT !!! GENERAL EXCEPTION !!! " & ex.Message)
            End Try
        End Function




        ' This method is called by Inventor when the AddIn is unloaded. The AddIn will be
        ' unloaded either manually by the user OrElse when the Inventor session is terminated.
        Public Sub Deactivate() Implements ApplicationAddInServer.Deactivate
            ' True programmatic self-unload of Add-In is not possible - https://adndevblog.typepad.com/manufacturing/2013/10/unload-net-addin-without-closing-inventor.html

            ' Release objects:
            cts.Cancel()

            AppEvents = Nothing
            UiEvents = Nothing
            invApp = Nothing

            GC.Collect()
            GC.WaitForPendingFinalizers()
        End Sub


        ' This property is provided to allow the AddIn to expose an API of its own to other 
        ' programs. Typically, this  would be done by implementing the AddIn's API
        ' interface in a class and returning that class object through this property.
        Public ReadOnly Property Automation() As Object Implements ApplicationAddInServer.Automation
            Get
                Return Nothing
            End Get
        End Property


        ' Note:this method is now obsolete, you should use the 
        ' ControlDefinition functionality for implementing commands.
        Public Sub ExecuteCommand(ByVal commandID As Integer) Implements ApplicationAddInServer.ExecuteCommand
        End Sub

    End Class
End Namespace