using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveLFS
{
    // { "event": "init", "operation": "download", "remote": "origin", "concurrent": true, "concurrenttransfers": 3 }
    // { "event": "upload", "oid": "bf3e3e2af9366a3b704ae0c31de5afa64193ebabffde2091936ad2e7510bc03a", "size": 73769, "path": "TestImg1.jpg", "action": { "href": "nfs://server/path", "header": { "key": "value" } } }
    // { "event": "download", "oid": "bf3e3e2af9366a3b704ae0c31de5afa64193ebabffde2091936ad2e7510bc03a", "size": 73769, "action": { "href": "nfs://server/path", "header": { "key": "value" } } }
    // { "event": "terminate" }   

    // { "event": "progress", "oid": "22ab5f63670800cc7be06dbed816012b0dc411e774754c7579467d2536a9cf3e", "bytesSoFar": 1234, "bytesSinceLast": 64 }
    // { "event": "complete", "oid": "bf3e3e2af9366a3b704ae0c31de5afa64193ebabffde2091936ad2e7510bc03a" }
    // { "event": "complete", "oid": "22ab5f63670800cc7be06dbed816012b0dc411e774754c7579467d2536a9cf3e", "error": { "code": 2, "message": "Explain what happened to this transfer" } }
    // { "event": "complete", "oid": "22ab5f63670800cc7be06dbed816012b0dc411e774754c7579467d2536a9cf3e", "path": "/path/to/file.png" }

    public enum CommandKind
    {
        none, init, upload, download, terminate, progress, complete
    }

    public enum OperationKind
    {
        none, download, upload
    }

    [Serializable]
    public struct ErrorData
    {
        public int code;
        public string message;
    }

    [Serializable]
    public struct ActionData
    {
        public string href;
        public Dictionary<string, string> header;
    }

    [Serializable]
    public struct CommandData
    {
        public CommandKind @event;
        public OperationKind operation;
        public string remote;
        public bool concurrent;
        public int concurrenttransfers;
        public string oid;
        public long size;
        public string path;
        public ActionData action;

        public long bytesSoFar;
        public long bytesSinceLast;
        public ErrorData error;
    }
}
