﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal
{
    public class IOCompletionThread
    {
        private readonly Thread _thread;

        public IOCompletionThread()
        {
            _thread = new Thread(ThreadStart);
            _thread.IsBackground = true;
            _thread.Name = nameof(SocketConnection);
            _thread.Start(this);
        }

        public IntPtr CompletionPort { get; } = CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 0);

        private static void ThreadStart(object parameter)
        {
            ((IOCompletionThread)parameter).DoIOCompletion();
        }

        private unsafe void DoIOCompletion()
        {
            while (true)
            {
                uint bytesTransferred;
                NativeOverlapped* nativeOverlapped;

                var result = GetQueuedCompletionStatus(
                    CompletionPort,
                    out bytesTransferred,
                    out _,
                    &nativeOverlapped,
                    uint.MaxValue);

                var overlapped = Overlapped.Unpack(nativeOverlapped);

                if (result)
                {
                    var asyncResult = (CallbackAsyncResult)overlapped.AsyncResult;
                    asyncResult.Callback(bytesTransferred, (IntPtr)nativeOverlapped, asyncResult.AsyncState);
                }
                else
                {
                    Trace.Assert(false, $"Unexpectedly failed. errorCode: '{Marshal.GetLastWin32Error()}'");
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateIoCompletionPort(
            IntPtr fileHandle,
            IntPtr existingCompletionPort,
            uint completionKey,
            uint numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static unsafe extern bool GetQueuedCompletionStatus(
            IntPtr CompletionPort,
            out uint lpNumberOfBytes,
            out UIntPtr lpCompletionKey,
            NativeOverlapped** lpOverlapped,
            uint dwMilliseconds);
    }
}