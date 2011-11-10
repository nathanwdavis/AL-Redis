<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Frame4.aspx.cs" Inherits="TestWebSite.Frame4" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
        LastPage: <%= Session["LastPage"].ToString() %>
        <br />
        UtcNow: <%= ((DateTime)Session["UtcNow"]).ToLongTimeString() +" ."+ ((DateTime)Session["UtcNow"]).Millisecond %>
    </div>
    </form>
</body>
</html>
