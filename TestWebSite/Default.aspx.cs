using System;
using System.Web;
using System.Web.UI;

namespace TestWebSite
{
	public partial class Default : System.Web.UI.Page
	{
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Session["LastPage"] = "Default";
            Session["UtcNow"] = DateTime.UtcNow;
            Session["BigObject"] = new ExponentiallyChunkyThing(2);
        }

	}
}

