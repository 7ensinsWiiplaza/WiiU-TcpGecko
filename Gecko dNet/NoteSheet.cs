using System;

namespace GeckoApp
{
    public class Sheet
    {

        public String title { get; set; }

        public String content { get; set; }

        public NotePage control { get; set; } = null;

        public override String ToString()
        {
            return title;
        }

        public Sheet(String title, String content)
        {
            this.title = title;
            this.content = content;
        }

        public Sheet(String title)
            : this(title, string.Empty)
        { }

        public Sheet()
            : this("New sheet", string.Empty)
        { }
    }
}
