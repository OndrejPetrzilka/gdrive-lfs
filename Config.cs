using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveLFS
{
    [Serializable]
    public struct Config
    {
        public string type;
        public string project_id;
        public string private_key_id;
        public string private_key;
        public string client_email;
        public string client_id;
        public string auth_uri;
        public string token_uri;
        public string auth_provider_x509_cert_url;
        public string client_x509_cert_url;
        public string universe_domain;
        public string log_path;
        public string drive_id;
        public string[] input_files;
        public bool attach_debugger;
    }
}
