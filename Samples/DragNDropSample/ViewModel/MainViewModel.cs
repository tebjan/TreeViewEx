﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DragNDrop;
using System.Windows.Input;
using DragNDropSample.Model;

namespace DragNDropSample.ViewModel
{
    public class MainViewModel
    {
        public ObservableCollection<Node> Children { get; set; }

        #region Drag & Drop Commands

        RelayCommand _dragCommand = null;
        public ICommand DragCommand
        {
            get
            {
                if (_dragCommand == null)
                    _dragCommand = new RelayCommand(ExecuteDrag, CanExecuteDrag);
                return _dragCommand;
            }
        }

        RelayCommand _dropCommand = null;
        public ICommand DropCommand
        {
            get
            {
                if (_dropCommand == null)
                    _dropCommand = new RelayCommand(ExecuteDrop, CanExecuteDrop);
                return _dropCommand;
            }
        }
        
        #endregion


        public MainViewModel()
		{
        	Children = new ObservableCollection<Node>();

            var first1 = new Node { Name = "element1" };
            var first2 = new Node { Name = "element2 (Drop Allowed)", AllowDrop = true };
            var first11 = new Node { Name = "element11 (Drag Allowed)", AllowDrag = true };
            var first12 = new Node { Name = "element12 (Drag Allowed)", AllowDrag = true };
            var first13 = new Node { Name = "element13 (Insert Allowed)", AllowInsert = true };
            var first14 = new Node { Name = "element14 (Drop Allowed)", AllowDrop = true };
            var first15 = new Node { Name = "element15" };
            var first131 = new Node { Name = "element131" };
            var first132 = new Node { Name = "element132 (Drop Allowed)", AllowDrop = true };

            Children.Add(first1);
            Children.Add(first2);
            first1.Children.Add(first11);
            first1.Children.Add(first12);
            first1.Children.Add(first13);
            first1.Children.Add(first14);
            first1.Children.Add(first15);
            first13.Children.Add(first131);
            first13.Children.Add(first132);
		}

        ObservableCollection<Node> _selectedItems = new ObservableCollection<Node>();
        public ObservableCollection<Node> SelectedItems
        {
            get { return _selectedItems; }
        }
        ReadOnlyObservableCollection<Node> _readonlySelectedItems = null;
        public ReadOnlyObservableCollection<Node> ReadOnlySelectedItems
        {
            get
            {
                if (_readonlySelectedItems == null)
                    _readonlySelectedItems = new ReadOnlyObservableCollection<Node>(_selectedItems);
                return _readonlySelectedItems;
            }
        }


        private bool CanExecuteDrag(object parameter)
        {
            DragParameters dragParameters = (DragParameters)parameter;
            TreeViewExItem tvei = dragParameters.DragItem;
            Node node = tvei.DataContext as Node;
            if(node!=null)
            {
                return node.AllowDrag;
            }
            return false;
        }
        private void ExecuteDrag(object parameter)
        {
            DragParameters dragParameters = (DragParameters)parameter;
            TreeViewExItem tvei = dragParameters.DragItem;
            dragParameters.DraggedObject = tvei.DataContext;
            return;
        }

        private bool CanExecuteDrop(object parameter)
        {
            DropParameters dropParameters = (DropParameters)parameter;
            TreeViewExItem tvei = dropParameters.DropToItem;
            IDataObject dataObject = dropParameters.DropData as IDataObject;
            int index = dropParameters.Index;            
            Node node = (tvei == null)?null:tvei.DataContext as Node;

            if (tvei == null) return true; //drop to root
            
            if (index == -1)
                return node.AllowDrop;
            else
                return node.AllowInsert;
        }

        private void ExecuteDrop(object parameter)
        {
            DropParameters dropParameters = (DropParameters)parameter;
            TreeViewExItem tvei = dropParameters.DropToItem;
            IDataObject dataObject = dropParameters.DropData as IDataObject;
            int index = dropParameters.Index;
            Node node = (tvei == null)?null:tvei.DataContext as Node;
            
            foreach (string f in dataObject.GetFormats())
            {
                object obj = dataObject.GetData(f);
                DragContent content = obj as DragContent;
                if (content != null)
                {
                    foreach (var item in content.Items.Reverse())
                    {
                        Node oldNode = (Node)item;
                        Node newNode = new Node();
                        newNode.Name = string.Format("Copy of {0}", oldNode.Name.Replace(" (Drag Allowed)", string.Empty));
                        AddNode(node, index, newNode);
                    }
                }
                else
                {
                    AddNode(node, index, new Node() { Name = "New node" });
                }                
            }
            return;
        }

        private void AddNode(Node node, int index, Node newNode)
        {
            ObservableCollection<Node> children = (node==null) ? this.Children: node.Children;
            if (index == -1)
                children.Add(newNode); //drop
            else
                children.Insert(index, newNode); //insert
        }
        
    }
}
