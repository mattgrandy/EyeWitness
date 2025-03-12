using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace EyeWitness
{
    /// <summary>
    ///  Borrowed and edited from the genius people at https://github.com/AppReadyGo/EyeTracker
    ///  They don't have a license so just be aware of that
    /// </summary>

    public class WebsiteSnapshot
    {
        private string Url { get; set; }
        private int? BrowserWidth { get; set; }
        private int? BrowserHeight { get; set; }
        private Bitmap Bitmap { get; set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public WebsiteSnapshot(string url, int? browserWidth = null, int? browserHeight = null)
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            this.Url = url;

            if (browserHeight == null && browserWidth == null)
            {
                this.BrowserHeight = bounds.Height;
                this.BrowserWidth = bounds.Width;
            }
            else
            {
                this.BrowserWidth = browserWidth;
                this.BrowserHeight = browserHeight;
            }
        }

        public Bitmap GenerateWebSiteImage(int timeout = 30000)
        {
            Thread thread = null;
            try
            {
                thread = new Thread(_GenerateWebSiteImage);
                thread.SetApartmentState(ApartmentState.STA);
                _resetEvent.Reset();

                thread.Start();

                // Use WaitOne instead of Join to better handle timeouts
                if (!_resetEvent.WaitOne(timeout))
                {
                    // Timeout occurred
                    Console.WriteLine("[-] Timeout occurred while generating screenshot, aborting...");
                    try
                    {
                        if (thread.IsAlive)
                            thread.Abort();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[-] Error while aborting thread: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Issue with thread, aborting: {ex.Message}");
                try
                {
                    if (thread != null && thread.IsAlive)
                        thread.Abort();
                }
                catch
                {
                    // Ignore exceptions during abort
                }
            }

            return Bitmap;
        }

        private void _GenerateWebSiteImage()
        {
            try
            {
                using (WebBrowser webBrowser = new WebBrowser { ScrollBarsEnabled = false, Visible = false })
                {
                    webBrowser.Hide();
                    webBrowser.ScriptErrorsSuppressed = true;
                    webBrowser.ScrollBarsEnabled = false;

                    // Add the document completed handler before navigating
                    webBrowser.DocumentCompleted += WebBrowserDocumentCompleted;

                    try
                    {
                        webBrowser.Navigate(Url, "_self");

                        // Give it time to start loading
                        Thread.Sleep(1000);

                        // Process Windows messages to allow the browser to load
                        DateTime startTime = DateTime.Now;
                        while (webBrowser.ReadyState != WebBrowserReadyState.Complete)
                        {
                            Application.DoEvents();

                            // Add a safety timeout within the thread
                            if ((DateTime.Now - startTime).TotalSeconds > 25)
                            {
                                break;
                            }

                            // Small delay to reduce CPU usage
                            Thread.Sleep(100);
                        }

                        // Ensure we have size values
                        if (!BrowserWidth.HasValue || !BrowserHeight.HasValue)
                        {
                            // Try to get the body dimensions
                            if (webBrowser.Document?.Body != null)
                            {
                                if (!BrowserWidth.HasValue)
                                    BrowserWidth = webBrowser.Document.Body.ScrollRectangle.Width + webBrowser.Margin.Horizontal;

                                if (!BrowserHeight.HasValue)
                                    BrowserHeight = webBrowser.Document.Body.ScrollRectangle.Height + webBrowser.Margin.Vertical;
                            }
                            else
                            {
                                // Default to reasonable values if not available
                                if (!BrowserWidth.HasValue)
                                    BrowserWidth = 1024;

                                if (!BrowserHeight.HasValue)
                                    BrowserHeight = 768;
                            }
                        }

                        // Set the size
                        webBrowser.ClientSize = new Size(BrowserWidth.Value, BrowserHeight.Value);

                        // Create bitmap in a safe way
                        // Using BeginInvoke to ensure UI thread safety for bitmap creation
                        if (webBrowser.IsHandleCreated)
                        {
                            webBrowser.Invoke(new Action(() =>
                            {
                                try
                                {
                                    // Create the bitmap with appropriate dimensions
                                    using (Bitmap tempBitmap = new Bitmap(Math.Max(1, webBrowser.Bounds.Width),
                                                                      Math.Max(1, webBrowser.Bounds.Height)))
                                    {
                                        // Draw the browser to the bitmap
                                        webBrowser.DrawToBitmap(tempBitmap, webBrowser.Bounds);

                                        // Assign to the class member (clone to avoid disposal issues)
                                        Bitmap = new Bitmap(tempBitmap);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[-] Error capturing bitmap: {ex.Message}");
                                }
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[-] Error in browser navigation: {ex.Message}");
                    }
                    finally
                    {
                        // Important: Remove event handler to prevent memory leaks
                        webBrowser.DocumentCompleted -= WebBrowserDocumentCompleted;

                        if (!webBrowser.IsDisposed)
                            webBrowser.Dispose();

                        // Signal that we're done
                        _resetEvent.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Fatal error in _GenerateWebSiteImage: {ex.Message}");
                _resetEvent.Set();
            }
        }

        private void SaveBitmap(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // This method is not used in the current implementation
            // But kept for reference
        }

        private void WebBrowserDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            // This event will be called when the document has finished loading
            try
            {
                WebBrowser webBrowser = sender as WebBrowser;
                if (webBrowser == null || webBrowser.IsDisposed)
                    return;

                // Update browser dimensions if needed
                if (!BrowserWidth.HasValue)
                {
                    if (webBrowser.Document?.Body != null)
                        BrowserWidth = webBrowser.Document.Body.ScrollRectangle.Width + webBrowser.Margin.Horizontal;
                }

                if (!BrowserHeight.HasValue)
                {
                    if (webBrowser.Document?.Body != null)
                        BrowserHeight = webBrowser.Document.Body.ScrollRectangle.Height + webBrowser.Margin.Vertical;
                }

                // Set the size if we have values
                if (BrowserWidth.HasValue && BrowserHeight.HasValue)
                    webBrowser.ClientSize = new Size(BrowserWidth.Value, BrowserHeight.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error in DocumentCompleted handler: {ex.Message}");
            }

            // Note: We don't dispose the browser here as it's handled in the main method
            // and doing it here could cause issues with the bitmap capture
        }
    }
}