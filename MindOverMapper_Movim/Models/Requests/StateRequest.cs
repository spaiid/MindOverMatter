using System;
using System.Collections.Generic;

namespace MindOverMapper_Movim.Models
{
    public partial class StateRequest
    {
        public int version { get; set; }
        public MapStateRequest state { get; set; }
    }
}
