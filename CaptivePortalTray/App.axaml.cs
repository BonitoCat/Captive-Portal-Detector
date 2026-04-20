using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Xaml;
using NotifyLib;
using NotifyLib.Avalonia;

namespace CaptivePortalTray;

public partial class App : Application
{
    class CaptivePortal()
    {
        public Uri? Url { get; set; }
        public int StatusCode { get; set; } = -1;
        public required bool Success { get; set; }
    }
    
    private bool _wasPortal = false;
    private CancellationTokenSource? _cts;
    
    private static readonly HttpClient Client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(5),
    };
    
    public override void Initialize()
    {
        #if !DEBUG
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                Environment.Exit(0);
            }
        #endif
        
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        NetworkChange.NetworkAddressChanged += (_, _) =>
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000, _cts.Token);
                    await DetectPortal(null, null);
                }
                catch (TaskCanceledException) { }
            });
        };
        
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await DetectPortal(null, null);
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        });
        
        base.OnFrameworkInitializationCompleted();
    }

    private async Task DetectPortal(object? sender, EventArgs? e)
    {
        CaptivePortal portal = await ScanForPortalAsync();
        if (portal.Success && !_wasPortal)
        {
            _wasPortal = true;

            await SendNotification("A Captive Portal has been found", ["Open in Browser"], (_, _) =>
            {
                OpenUrl(portal.Url);
            });
        }
        else if (!portal.Success)
        {
            _wasPortal = false;
        }
    }

    private async Task<CaptivePortal> ScanForPortalAsync()
    {
        HttpResponseMessage response;
        try
        {
            response = await Client.GetAsync("http://detectportal.firefox.com/success.txt");
        }
        catch (Exception)
        {
            return new() { Success = false };
        }

        int status = (int) response.StatusCode;
        Console.WriteLine(status);
        
        if (status >= 300 && status < 400)
        {
            Uri? portalUrl = response.Headers.Location;
            if (portalUrl is null)
            {
                return new()
                {
                    StatusCode = status,
                    Success = false,
                };
            }
            
            if (!portalUrl.IsAbsoluteUri && response.RequestMessage?.RequestUri is not null)
            {
                portalUrl = new Uri(response.RequestMessage.RequestUri, portalUrl);
            }

            Console.WriteLine(portalUrl);
            return new()
            {
                Url = portalUrl,
                StatusCode = status,
                Success = true,
            };
        }
        else if (status >= 200 && status < 300)
        {
            return new()
            {
                StatusCode = status,
                Success = false,
            };
        }

        return new() { Success = false };
    }

    private void OpenUrl(Uri? portalUrl)
    {
        if (portalUrl is null) return;
        
        Process.Start(new ProcessStartInfo
        {
            FileName = portalUrl.AbsoluteUri,
            UseShellExecute = true,
        });
    }
    
    private async void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        CaptivePortal portal = await ScanForPortalAsync();
        if (portal.Success && portal.Url is not null)
        {
            await SendNotification("Found a Captive Portal");
        }
        else
        {
            await SendNotification("Could not find a Captive Portal");
        }

        OpenUrl(portal.Url);
    }

    private async Task<Notification> SendNotification(string message, List<string>? actions = null, Action<object, ActionClickedArgs>? onActionClicked = null)
    {
        Notification notification = new()
        {
            Title = "Captive Portal Detector",
            Message = message,
            Actions = actions,
        };
        
        notification.SetIconFromUri(new Uri("avares://CaptivePortalTray/Assets/CaptivePortal.ico"));
        notification.OnActionClicked += onActionClicked;
        
        await notification.Show();
        notification.OnActionClicked -= onActionClicked;

        return notification;
    }

    private void Quit_OnClick(object? sender, EventArgs e)
    {
        Environment.Exit(0);
    }
}