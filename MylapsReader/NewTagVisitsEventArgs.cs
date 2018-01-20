namespace MylapsReader
{
    using System;

    public class NewTagVisitsEventArgs : EventArgs
    {
        public string Name { get; set; }

        public long Sequence { get; set; }

        public TagVisit[] TagVisits { get; set; }
    }
}
