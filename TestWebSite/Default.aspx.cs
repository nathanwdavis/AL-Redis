using System;
using System.Web;
using System.Web.UI;

namespace TestWebSite
{
	public partial class Default : System.Web.UI.Page
	{
		
		public virtual void button1Clicked (object sender, EventArgs args)
		{
			Session["test"] = "This is a test";
			Session["someTime"] = DateTime.UtcNow;
			button1.Text = "You clicked me";
		}
	}
}

