﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DIPOL_UF.Views
{
    /// <summary>
    /// Interaction logic for ProgressWindow.xaml
    /// </summary>
    public partial class ProgressWindow : Window
    {
       

        public ProgressWindow(object dataContext)
        {
            var x = Application.Current.Resources.Values;
            InitializeComponent();
            DataContext = dataContext;
            Loaded += ProgressWindow_Loaded;
          
        }

        private void ProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
