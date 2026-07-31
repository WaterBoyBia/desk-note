using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace DeskNote.App.Services;

public sealed class TrayIconController : IDisposable
{
    private readonly ContextMenuStrip menu;
    private readonly Icon trayIcon;
    private readonly NotifyIcon notifyIcon;

    public TrayIconController(Action show, Action create, Action exit)
    {
        menu = new ContextMenuStrip();
        menu.Items.Add("显示 desk-note", null, (_, _) => show());
        menu.Items.Add("创建新待办", null, (_, _) => create());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        trayIcon = LoadTrayIcon();
        notifyIcon = new NotifyIcon
        {
            Text = "desk-note",
            Icon = trayIcon,
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
        menu.Dispose();
        trayIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var resource = Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/desk-note.ico"));
            if (resource is null)
            {
                return (Icon)SystemIcons.Application.Clone();
            }

            using var stream = resource.Stream;
            using var source = new Icon(stream);
            return (Icon)source.Clone();
        }
        catch (IOException)
        {
            return (Icon)SystemIcons.Application.Clone();
        }
        catch (ArgumentException)
        {
            return (Icon)SystemIcons.Application.Clone();
        }
    }
}
