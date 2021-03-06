﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using LiteDbExplorer.Windows;
using LiteDB;

namespace LiteDbExplorer.Controls
{
    public class DocumentFieldData
    {
        public string Name
        {
            get; set;
        }

        public FrameworkElement EditControl
        {
            get; set;
        }

        public DocumentFieldData(string name, FrameworkElement editControl)
        {
            Name = name;
            EditControl = editControl;
        }
    }

    /// <summary>
    ///     Interaction logic for DocumentViewerControl.xaml
    /// </summary>
    public partial class DocumentViewerControl : UserControl
    {
        public static readonly RoutedUICommand PreviousItem = new RoutedUICommand
        (
            "Previous Item",
            "PreviousItem",
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.PageUp)
            }
        );

        public static readonly RoutedUICommand NextItem = new RoutedUICommand
        (
            "Next Item",
            "NextItem",
            typeof(Commands),
            new InputGestureCollection
            {
                new KeyGesture(Key.PageDown)
            }
        );

        private BsonDocument currentDocument;

        private ObservableCollection<DocumentFieldData> customControls;
        private DocumentReference documentReference;

        private bool _loaded = false;

        private DocumentViewerControl(WindowController windowController = null)
        {

            _windowController = windowController;

            InitializeComponent();

            ListItems.Loaded += (sender, args) =>
            {
                if (_loaded)
                {
                    return;
                }

                InvalidateItemsSize();

                _loaded = true;
            };
        }

        public DocumentViewerControl(BsonDocument document, bool readOnly, WindowController windowController = null) : this(windowController)
        {
            IsReadOnly = readOnly;

            currentDocument = document;
            customControls = new ObservableCollection<DocumentFieldData>();

            for (var i = 0; i < document.Keys.Count; i++)
            {
                var key = document.Keys.ElementAt(i);
                customControls.Add(NewField(key, readOnly));
            }

            ListItems.ItemsSource = customControls;

            ButtonNext.Visibility = Visibility.Collapsed;
            ButtonPrev.Visibility = Visibility.Collapsed;

            if (readOnly)
            {
                ButtonClose.Visibility = Visibility.Visible;
                ButtonOK.Visibility = Visibility.Collapsed;
                ButtonCancel.Visibility = Visibility.Collapsed;
                DropNewField.Visibility = Visibility.Collapsed;
            }
        }

        public DocumentViewerControl(DocumentReference document, WindowController windowController = null) : this(windowController)
        {
            LoadDocument(document);
        }

        public bool IsReadOnly { get; }

        public bool DialogResult { get; set; }

        private void LoadDocument(DocumentReference document)
        {
            if (document.Collection is FileCollectionReference)
            {
                var fileInfo = (document.Collection as FileCollectionReference).GetFileObject(document);
                GroupFile.Visibility = Visibility.Visible;
                FileView.LoadFile(fileInfo);
            }

            currentDocument = document.Collection.LiteCollection.FindById(document.LiteDocument["_id"]);
            documentReference = document;
            customControls = new ObservableCollection<DocumentFieldData>();

            for (var i = 0; i < document.LiteDocument.Keys.Count; i++)
            {
                var key = document.LiteDocument.Keys.ElementAt(i);
                customControls.Add(NewField(key, IsReadOnly));
            }

            ListItems.ItemsSource = customControls;
        }

        private DocumentFieldData NewField(string key, bool readOnly)
        {
            var expandMode = OpenEditorMode.Inline;
            if (_windowController != null)
            {
                expandMode = OpenEditorMode.Window;
            }

            var valueEdit =
                BsonValueEditor.GetBsonValueEditor(
                    openMode: expandMode, 
                    bindingPath: $"[{key}]", 
                    bindingValue: currentDocument[key], 
                    bindingSource: currentDocument, 
                    readOnly: readOnly, 
                    keyName: key);

            return new DocumentFieldData(key, valueEdit);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var key = (sender as Button).Tag as string;
            var item = customControls.First(a => a.Name == key);
            customControls.Remove(item);
            currentDocument.Remove(key);
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event EventHandler CloseRequested;

        private void Close()
        {
            OnCloseRequested();

            _windowController?.Close(DialogResult);
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            //TODO make array and document types use this as well
            foreach (var ctrl in customControls)
            {
                var control = ctrl.EditControl;
                var values = control.GetLocalValueEnumerator();
                while (values.MoveNext())
                {
                    var current = values.Current;
                    if (BindingOperations.IsDataBound(control, current.Property))
                    {
                        var binding = control.GetBindingExpression(current.Property);
                        if (binding.IsDirty) binding.UpdateSource();
                    }
                }
            }

            if (documentReference != null)
            {
                documentReference.LiteDocument = currentDocument;
                documentReference.Collection.UpdateItem(documentReference);
            }

            DialogResult = true;
            Close();
        }

        private void NewFieldMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (InputBoxWindow.ShowDialog("Enter name of new field.", "New field name:", "", out var fieldName) !=
                true) return;

            if (currentDocument.Keys.Contains(fieldName))
            {
                MessageBox.Show(string.Format("Field \"{0}\" already exists!", fieldName), "", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var menuItem = sender as MenuItem;
            BsonValue newValue;

            switch (menuItem.Header as string)
            {
                case "String":
                    newValue = new BsonValue(string.Empty);
                    break;
                case "Boolean":
                    newValue = new BsonValue(false);
                    break;
                case "Double":
                    newValue = new BsonValue((double) 0);
                    break;
                case "Int32":
                    newValue = new BsonValue(0);
                    break;
                case "Int64":
                    newValue = new BsonValue((long) 0);
                    break;
                case "DateTime":
                    newValue = new BsonValue(DateTime.MinValue);
                    break;
                case "Array":
                    newValue = new BsonArray();
                    break;
                case "Document":
                    newValue = new BsonDocument();
                    break;
                default:
                    throw new Exception("Uknown value type.");
            }

            currentDocument.Add(fieldName, newValue);
            var newField = NewField(fieldName, false);
            customControls.Add(newField);
            newField.EditControl.Focus();
            ItemsField_SizeChanged(ListItems, null);
            ListItems.ScrollIntoView(newField);
        }

        private bool _invalidatingSize;
        private readonly WindowController _windowController;

        private async void ItemsField_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_invalidatingSize)
            {
                return;
            }

            _invalidatingSize = true;

            var listView = sender as ListView;
            var grid = listView.View as GridView;
            var newWidth = listView.ActualWidth - SystemParameters.VerticalScrollBarWidth - 10 -
                           grid.Columns[0].ActualWidth - grid.Columns[2].ActualWidth;

            if (newWidth > 0)
            {
                grid.Columns[1].Width = newWidth;
            }

            if (_loaded)
            {
                await Task.Delay(50);
            }
            
            _invalidatingSize = false;
        }

        private void InvalidateItemsSize()
        {
            ItemsField_SizeChanged(ListItems, null);
        }

        private void NextItemCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (documentReference == null)
                e.CanExecute = false;
            else
            {
                var index = documentReference.Collection.Items.IndexOf(documentReference);
                e.CanExecute = index + 1 < documentReference.Collection.Items.Count;
            }
        }

        private void NextItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var index = documentReference.Collection.Items.IndexOf(documentReference);

            if (index + 1 < documentReference.Collection.Items.Count)
            {
                var newDocument = documentReference.Collection.Items[index + 1];
                LoadDocument(newDocument);
            }
        }

        private void PreviousItemCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (documentReference == null)
                e.CanExecute = false;
            else
            {
                var index = documentReference.Collection.Items.IndexOf(documentReference);
                e.CanExecute = index > 0;
            }
        }

        private void PreviousItemCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var index = documentReference.Collection.Items.IndexOf(documentReference);

            if (index > 0)
            {
                var newDocument = documentReference.Collection.Items[index - 1];
                LoadDocument(newDocument);
            }
        }

        protected virtual void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}