using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sap_profile_ms.Model.ViewModels
{
    public class ViewModelResponse
    {
        public bool Error { get; set; }
        public string Response { get; set; }
        public ViewModelUser User { get; set; }
        public object Token { get; set; }
        public string Uri { get; set; }
    }
}
