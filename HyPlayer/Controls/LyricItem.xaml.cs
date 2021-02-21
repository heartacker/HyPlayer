﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using HyPlayer.Classes;
using Microsoft.UI.Xaml.Media;
using AcrylicBackgroundSource = Windows.UI.Xaml.Media.AcrylicBackgroundSource;
using AcrylicBrush = Microsoft.UI.Xaml.Media.AcrylicBrush;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace HyPlayer.Controls
{
    public sealed partial class LyricItem : UserControl
    {
        public readonly SongLyric Lrc;
        private int showsize = 20;
        private int hidsize = 14;
        private bool showing = false;
        public LyricItem(SongLyric lrc)
        {

            this.InitializeComponent();
            Window.Current.SizeChanged += Current_SizeChanged;
            Current_SizeChanged(null, null);
            Lrc = lrc;
            TextBoxPureLyric.Text = Lrc.PureLyric;
            if (Lrc.HaveTranslation)
            {
                TextBoxTranslation.Text = Lrc.Translation;
            }
            else
            {
                TextBoxTranslation.Visibility = Visibility.Collapsed;
            }
        }

        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {

            showsize = Math.Max((int)Window.Current.Bounds.Width / 50, 18);
            hidsize = Math.Max((int)Window.Current.Bounds.Width / 80, 14);
            if (showing)
            {
                TextBoxTranslation.FontSize = showsize;
                TextBoxPureLyric.FontSize = showsize;
            }
            else
            {
                TextBoxTranslation.FontSize = hidsize;
                TextBoxPureLyric.FontSize = hidsize;
            }

        }

        public void OnShow()
        {
            showing = true;
            TextBoxPureLyric.FontWeight = FontWeights.ExtraBold;
            TextBoxTranslation.FontWeight = FontWeights.ExtraBold;
            TextBoxTranslation.FontSize = showsize;
            TextBoxPureLyric.FontSize = showsize;
        }

        public void OnHind()
        {
            showing = false;
            TextBoxPureLyric.FontWeight = FontWeights.Normal;
            TextBoxTranslation.FontWeight = FontWeights.Normal;
            TextBoxTranslation.FontSize = hidsize;
            TextBoxPureLyric.FontSize = hidsize;
        }
    }
}