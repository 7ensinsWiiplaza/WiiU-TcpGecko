using System;

namespace GeckoApp
{
    public class Sheet
    {
        private String PTitle;
        private String PContent;
        private NotePage PControl = null;

        public String title
        {
            get { return PTitle; }
            set { PTitle = value; }
        }

        public String content
        {
            get { return PContent; }
            set { PContent = value; }
        }

        public NotePage control
        {
            get { return PControl; }
            set { PControl = value; }
        }

        public override String ToString()
        {
            return PTitle;
        }

        public Sheet(String title, String content)
        {
            PTitle = title;
            PContent = content;
        }

        public Sheet(String title)
            : this(title, string.Empty)
        { }

        public Sheet()
            : this("New sheet", string.Empty)
        { }
    }
}