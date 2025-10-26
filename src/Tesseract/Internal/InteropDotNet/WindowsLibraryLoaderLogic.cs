//  Copyright (c) 2014 Andrey Akinshin
//  Project URL: https://github.com/AndreyAkinshin/InteropDotNet
//  Distributed under the MIT License: http://opensource.org/licenses/MIT
using System;
using System.Runtime.InteropServices;
using System.Text;
using Tesseract.Internal;

namespace InteropDotNet
{
    class WindowsLibraryLoaderLogic : ILibraryLoaderLogic
    {
        public IntPtr LoadLibrary(string fileName)
        {
            var libraryHandle = IntPtr.Zero;

            try
            {
                Logger.TraceInformation("Trying to load native library \"{0}\"...", fileName);
                
                // Try SetDllDirectory to help with dependent DLLs
                var directory = System.IO.Path.GetDirectoryName(fileName);
                if (!string.IsNullOrEmpty(directory))
                {
                    Logger.TraceInformation("Setting DLL directory to: \"{0}\"", directory);
                    SetDllDirectory(directory);
                }
                
                libraryHandle = WindowsLoadLibrary(fileName);
                if (libraryHandle != IntPtr.Zero)
                {
                    Logger.TraceInformation("Successfully loaded native library \"{0}\", handle = {1}.", fileName, libraryHandle);
                }
                else
                {
                    var lastError = WindowsGetLastError();
                    var errorMessage = GetLastErrorMessage(lastError);
                    Logger.TraceError("Failed to load native library \"{0}\".\r\nError Code: {1}\r\nError Message: {2}\r\nCheck windows event log.", 
                        fileName, lastError, errorMessage);
                    
                    // Try LoadLibraryEx with additional flags for more detailed error info
                    Logger.TraceInformation("Attempting LoadLibraryEx with LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR flag...");
                    libraryHandle = LoadLibraryEx(fileName, IntPtr.Zero, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
                    if (libraryHandle != IntPtr.Zero)
                    {
                        Logger.TraceInformation("Successfully loaded with LoadLibraryEx.");
                    }
                    else
                    {
                        lastError = WindowsGetLastError();
                        errorMessage = GetLastErrorMessage(lastError);
                        Logger.TraceError("LoadLibraryEx also failed. Error Code: {0}, Message: {1}", lastError, errorMessage);
                    }
                }
            }
            catch (Exception e)
            {
                var lastError = WindowsGetLastError();
                var errorMessage = GetLastErrorMessage(lastError);
                Logger.TraceError("Failed to load native library \"{0}\".\r\nLast Error:{1}\r\nError Message: {2}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {3}", 
                    fileName, lastError, errorMessage, e.ToString());
            }
            finally
            {
                // Reset DLL directory
                SetDllDirectory(null);
            }

            return libraryHandle;
        }

        public bool FreeLibrary(IntPtr libraryHandle)
        {
            try
            {
                Logger.TraceInformation("Trying to free native library with handle {0} ...", libraryHandle);
                var isSuccess = WindowsFreeLibrary(libraryHandle);
                if (isSuccess)
                    Logger.TraceInformation("Successfully freed native library with handle {0}.", libraryHandle);
                else
                {
                    var lastError = WindowsGetLastError();
                    var errorMessage = GetLastErrorMessage(lastError);
                    Logger.TraceError("Failed to free native library with handle {0}.\r\nError: {1} - {2}\r\nCheck windows event log.", 
                        libraryHandle, lastError, errorMessage);
                }
                return isSuccess;
            }
            catch (Exception e)
            {
                var lastError = WindowsGetLastError();
                var errorMessage = GetLastErrorMessage(lastError);
                Logger.TraceError("Failed to free native library with handle {0}.\r\nLast Error:{1}\r\nError Message: {2}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {3}", 
                    libraryHandle, lastError, errorMessage, e.ToString());
                return false;
            }
        }

        public IntPtr GetProcAddress(IntPtr libraryHandle, string functionName)
        {
            try
            {
                Logger.TraceInformation("Trying to load native function \"{0}\" from the library with handle {1}...",
                    functionName, libraryHandle);
                var functionHandle = WindowsGetProcAddress(libraryHandle, functionName);
                if (functionHandle != IntPtr.Zero)
                {
                    Logger.TraceInformation("Successfully loaded native function \"{0}\", function handle = {1}.",
                        functionName, functionHandle);
                }
                else
                {
                    var lastError = WindowsGetLastError();
                    var errorMessage = GetLastErrorMessage(lastError);
                    throw new Tesseract.LoadLibraryException(String.Format(
                        "Failed to load native function \"{0}\" from library with handle {1}.\r\nError: {2} - {3}",
                        functionName, libraryHandle, lastError, errorMessage));
                }
                return functionHandle;
            }
            catch (Exception e)
            {
                var lastError = WindowsGetLastError();
                var errorMessage = GetLastErrorMessage(lastError);
                throw new Tesseract.LoadLibraryException(
                    String.Format("Failed to load native function \"{0}\" from library with handle {1}.\r\nLast Error:{2}\r\nError Message: {3}\r\nCheck inner exception and\\or windows event log.\r\nInner Exception: {4}", 
                        functionName, libraryHandle, lastError, errorMessage, e.ToString()),
                    e);
            }
        }

        public string FixUpLibraryName(string fileName)
        {
            if (!String.IsNullOrEmpty(fileName) && !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return fileName + ".dll";
            return fileName;
        }

        private static string GetLastErrorMessage(int errorCode)
        {
            var sb = new StringBuilder(256);
            var len = FormatMessage(
                FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                IntPtr.Zero,
                (uint)errorCode,
                0,
                sb,
                sb.Capacity,
                IntPtr.Zero);
            
            if (len > 0)
            {
                return sb.ToString().Trim();
            }
            return $"Unknown error (0x{errorCode:X})";
        }

        // LoadLibraryEx flags
        private const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

        // FormatMessage flags
        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        private const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;

        [DllImport("kernel32", EntryPoint = "LoadLibrary", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr WindowsLoadLibrary(string dllPath);

        [DllImport("kernel32", EntryPoint = "LoadLibraryEx", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern IntPtr LoadLibraryEx(string dllPath, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32", EntryPoint = "SetDllDirectory", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", EntryPoint = "FreeLibrary", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        private static extern bool WindowsFreeLibrary(IntPtr handle);

        [DllImport("kernel32", EntryPoint = "GetProcAddress", CallingConvention = CallingConvention.Winapi,
            SetLastError = true)]
        private static extern IntPtr WindowsGetProcAddress(IntPtr handle, string procedureName);

        [DllImport("kernel32", EntryPoint = "FormatMessage", CallingConvention = CallingConvention.Winapi,
            SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId,
            uint dwLanguageId, StringBuilder lpBuffer, int nSize, IntPtr Arguments);

        private static int WindowsGetLastError()
        {
            return Marshal.GetLastWin32Error();
        }
    }
}