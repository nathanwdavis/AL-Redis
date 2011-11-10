<%@ Page Language="C#" Inherits="TestWebSite.Default" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Strict//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd">
<html>
<head runat="server">
	<title>Default</title>
</head>
<body>
	<form id="form1" runat="server">
		LastPage: <%= Session["LastPage"].ToString() %>
        <br />
        UtcNow: <%= ((DateTime)Session["UtcNow"]).ToLongTimeString() +" ."+ ((DateTime)Session["UtcNow"]).Millisecond %>
	</form>
    Frame1:
    <iframe width="100%" src="Frame1.aspx"></iframe>
    Frame2:
    <iframe width="100%" src="Frame2.aspx"></iframe>
    Frame3:
    <iframe width="100%" src="Frame3.aspx"></iframe>
    Frame4:
    <iframe width="100%" src="Frame4.aspx"></iframe>
    ReadonlyFrame:
    <iframe width="100%" src="ReadonlyFrame.aspx"></iframe>
</body>
</html>
