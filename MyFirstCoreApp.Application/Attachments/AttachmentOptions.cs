using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyFirstCoreApp.Application.Attachments
{
    public class AttachmentOptions
    {
        public string RootPath { get; set; }
        public long MaxFileSizeBytes { get; set; }
        public int MaxFilesPerEntity { get; set; }
    }
}
