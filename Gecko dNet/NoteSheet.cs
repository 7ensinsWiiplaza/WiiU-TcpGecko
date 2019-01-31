using System;

namespace GeckoApp
{
    public class Sheet
    {

        public string title { get; set; }

        public string content { get; set; }

        public NotePage control { get; set; } = null;

        public override string ToString()
        {
            return title;
        }

        public Sheet(string title, string content)
        {
            this.title = title;
            this.content = content;
        }

        public Sheet(string title)
            : this(title, string.Empty)
        { }

        public Sheet()
            : this("New sheet", string.Empty)
        { }
    }
}
