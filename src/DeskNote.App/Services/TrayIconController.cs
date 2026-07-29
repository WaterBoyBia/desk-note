using System.Drawing;
using System.Windows.Forms;

namespace DeskNote.App.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon notifyIcon;

    public TrayIconController(Action show, Action create, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("显示 desk-note", null, (_, _) => show());
        menu.Items.Add("创建新待办", null, (_, _) => create());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        notifyIcon = new NotifyIcon
        {
            Text = "desk-note",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                show();
            }
        };
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }
}
