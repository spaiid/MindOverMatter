using System;
using System.Collections.Generic;

namespace MindOverMapper_Movim.Models
{
    public partial class MapStateRequest
    {
        public string rootItemKey { get; set; }
        public string editorRootItemKey { get; set; }
        public List<StateItemRequest> items { get; set; }
    }
}
