﻿using System.Windows;
using System.Windows.Data;
using Caliburn.Micro;
using LiteDbExplorer.Framework.Windows;

namespace LiteDbExplorer.Framework
{
    public class AppWindowManager : WindowManager
    {
        /// <summary>
        /// Selects a base window depending on the view, model and dialog options
        /// </summary>
        /// <param name="model">The model</param>
        /// <param name="view">The view</param>
        /// <param name="isDialog">Whether it's a dialog</param>
        /// <returns>The proper window</returns>
        protected override Window EnsureWindow(object model, object view, bool isDialog)
        {
            Window window = view as BaseWindow;

            if (window == null)
            {
                if (isDialog)
                {
                    window = new BaseDialogWindow
                    {
                        Content = view,
                        SizeToContent = SizeToContent.WidthAndHeight
                    };
                }
                else
                {
                    window = new BaseWindow
                    {
                        Content = view,
                        SizeToContent = SizeToContent.Manual,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen,
                        ResizeMode = ResizeMode.CanResizeWithGrip
                    };
                    
                    ((BaseWindow) window).LeftWindowCommands
                        .SetBinding(FrameworkElement.DataContextProperty, new Binding
                        {
                            Path = new PropertyPath(nameof(FrameworkElement.DataContext)),
                            Source = view,
                            Mode = BindingMode.OneWay
                        });
                    
                    var windowName = view.GetType().FullName;

                    window.AttachPositionHandler(windowName);
                }
                
                window.SetValue(View.IsGeneratedProperty, true);
            }
            else
            {
                var owner = InferOwnerOf(window);
                if (owner != null && isDialog)
                {
                    window.Owner = owner;
                }
            }

            return window;
        }
    }
}