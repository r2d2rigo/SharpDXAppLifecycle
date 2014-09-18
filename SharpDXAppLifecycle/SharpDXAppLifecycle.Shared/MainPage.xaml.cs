///////////////////////////////////////////////////////////////////////////////
//
// Copyright (c) 2012-2014 Rodrigo 'r2d2rigo' Díaz
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
///////////////////////////////////////////////////////////////////////////////

using SharpDX;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;
using D2D1 = SharpDX.Direct2D1;
using Windows.Foundation;

namespace SharpDXAppLifecycle
{
    public sealed partial class MainPage : Page
    {
        private D3D11.Device2 device;
        private D3D11.DeviceContext2 deviceContext;
        private DXGI.SwapChain2 swapChain;
        private D3D11.Texture2D backBufferTexture;
        private D3D11.RenderTargetView backBufferView;
        private bool isDXInitialized;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void SwapChainPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // Create a new Direct3D hardware device and ask for Direct3D 11.2 support
            using (D3D11.Device defaultDevice = new D3D11.Device(D3D.DriverType.Hardware, D3D11.DeviceCreationFlags.Debug))
            {
                this.device = defaultDevice.QueryInterface<D3D11.Device2>();
            }

            // Save the context instance
            this.deviceContext = this.device.ImmediateContext2;

            Size2 sizeInPixels = RenderSizeToPixelSize(this.SwapChainPanel.RenderSize);

            // Properties of the swap chain
            DXGI.SwapChainDescription1 swapChainDescription = new DXGI.SwapChainDescription1()
            {
                // No transparency.
                AlphaMode = DXGI.AlphaMode.Ignore,
                // Double buffer.
                BufferCount = 2,
                // BGRA 32bit pixel format.
                Format = DXGI.Format.B8G8R8A8_UNorm,
                // Unlike in CoreWindow swap chains, the dimensions must be set.
                Height = sizeInPixels.Height,
                Width = sizeInPixels.Width,
                // Default multisampling.
                SampleDescription = new DXGI.SampleDescription(1, 0),
                // In case the control is resized, stretch the swap chain accordingly.
                Scaling = DXGI.Scaling.Stretch,
                // No support for stereo display.
                Stereo = false,
                // Sequential displaying for double buffering.
                SwapEffect = DXGI.SwapEffect.FlipSequential,
                // This swapchain is going to be used as the back buffer.
                Usage = DXGI.Usage.BackBuffer | DXGI.Usage.RenderTargetOutput,
            };

            // Retrive the DXGI device associated to the Direct3D device.
            using (DXGI.Device3 dxgiDevice3 = this.device.QueryInterface<DXGI.Device3>())
            {
                // Get the DXGI factory automatically created when initializing the Direct3D device.
                using (DXGI.Factory3 dxgiFactory3 = dxgiDevice3.Adapter.GetParent<DXGI.Factory3>())
                {
                    // Create the swap chain and get the highest version available.
                    using (DXGI.SwapChain1 swapChain1 = new DXGI.SwapChain1(dxgiFactory3, this.device, ref swapChainDescription))
                    {
                        this.swapChain = swapChain1.QueryInterface<DXGI.SwapChain2>();
                    }
                }
            }

            // Obtain a reference to the native COM object of the SwapChainPanel.
            using (DXGI.ISwapChainPanelNative nativeObject = ComObject.As<DXGI.ISwapChainPanelNative>(this.SwapChainPanel))
            {
                // Set its swap chain.
                nativeObject.SwapChain = this.swapChain;
            }

            // Create a Texture2D from the existing swap chain to use as 
            this.backBufferTexture = D3D11.Texture2D.FromSwapChain<D3D11.Texture2D>(this.swapChain, 0);
            this.backBufferView = new D3D11.RenderTargetView(this.device, this.backBufferTexture);

            // This event is fired when the application requests a new frame from the DirectX interop controls.
            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // Subscribe to the suspending event
            Application.Current.Suspending += Application_Suspending;

            // Mark our resources as initialized
            isDXInitialized = true;
        }

        private void CompositionTarget_Rendering(object sender, object e)
        {
            // Set the active back buffer and clear it.
            this.deviceContext.OutputMerger.SetRenderTargets(this.backBufferView);
            this.deviceContext.ClearRenderTargetView(this.backBufferView, Color.CornflowerBlue);

            // Tell the swap chain to present the buffer.
            this.swapChain.Present(1, DXGI.PresentFlags.None, new DXGI.PresentParameters());
        }

        private Size2 RenderSizeToPixelSize(Size renderSize)
        {
            // We have to take into account pixel scaling; Windows Phone 8.1 uses virtual resolutions smaller than the physical screen size.
            float pixelScale = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;

            return new Size2((int)(renderSize.Width * pixelScale), (int)(renderSize.Height * pixelScale));
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(BlankPage));
        }

        private void SwapChainPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from suspending event.
            App.Current.Suspending -= Application_Suspending;

            // Unsubscribe from rendering event.
            CompositionTarget.Rendering -= CompositionTarget_Rendering;

            // Unset native swap chanel's reference.
            using (DXGI.ISwapChainPanelNative nativeObject = ComObject.As<DXGI.ISwapChainPanelNative>(this.SwapChainPanel))
            {
                nativeObject.SwapChain = null;
            }

            // Safely dispose all resources.
            Utilities.Dispose(ref this.backBufferView);
            Utilities.Dispose(ref this.backBufferTexture);
            Utilities.Dispose(ref this.swapChain);
            Utilities.Dispose(ref this.deviceContext);
            Utilities.Dispose(ref this.device);
        }

        private void SwapChainPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Check if resources have been initialized.
            if (isDXInitialized)
            {
                Size2 newSize = RenderSizeToPixelSize(e.NewSize);

                // If the requested swap chain is bigger than the current one,
                if (newSize.Width > swapChain.Description1.Width || newSize.Height > swapChain.Description1.Height)
                {
                    // Destroy resources.
                    Utilities.Dispose(ref this.backBufferView);
                    Utilities.Dispose(ref this.backBufferTexture);

                    // Resize swap chain while conserving format and flags.
                    swapChain.ResizeBuffers(swapChain.Description.BufferCount, (int)e.NewSize.Width, (int)e.NewSize.Height, swapChain.Description1.Format, swapChain.Description1.Flags);

                    // Recreate resources.
                    this.backBufferTexture = D3D11.Texture2D.FromSwapChain<D3D11.Texture2D>(this.swapChain, 0);
                    this.backBufferView = new D3D11.RenderTargetView(this.device, this.backBufferTexture);
                }

                // Set source size propery
                swapChain.SourceSize = newSize;
            }
        }

        private void Application_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            // Check if resources have been initialized.
            if (isDXInitialized)
            {
                // Clear any object references.
                this.deviceContext.ClearState();

                // Trim the memory.
                using (DXGI.Device3 dxgiDevice3 = this.swapChain.GetDevice<DXGI.Device3>())
                {
                    dxgiDevice3.Trim();
                }
            }
        }
    }
}
