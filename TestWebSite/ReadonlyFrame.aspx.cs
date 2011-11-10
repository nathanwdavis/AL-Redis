using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace TestWebSite
{
    public partial class ReadonlyFrame : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Session["LastPage"] = "ReadonlyFrame";
            Session["UtcNow"] = DateTime.UtcNow;
            Session["BigObject"] = new ExponentiallyChunkyThing(4);
        }
    }
}