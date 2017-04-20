﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Input;

namespace System.Windows.Controls.DragNDrop
{
    using System.Linq;
    using System.Windows.Documents;
    using System.Windows.Media;
    using System.Windows.Threading;

    class DragNDropController : InputSubscriberBase, IDisposable
    {
        private AutoScroller autoScroller;

        private List<TreeViewExItem> draggableItems;
        
        Stopwatch stopWatch;

        InsertAdorner insertAdorner;

        const int dragAreaSize = 5;

        public DragNDropController(AutoScroller autoScroller)
        {
            this.autoScroller = autoScroller;
        }

        internal override void Initialized()
        {
            base.Initialized();
            TreeView.AllowDrop = true;

            TreeView.Drop += OnDrop;
            TreeView.DragOver += OnDragOver;
            TreeView.DragLeave += OnDragLeave;
        }

        void OnDragLeave(object sender, DragEventArgs e)
        {
            if (!IsMouseOverTreeView(e.GetPosition(TreeView)))
            {
                CleanUpAdorners();
            }
        }

        private bool IsMouseOverTreeView(Point pos)
        {
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(TreeView, pos);
            if (hitTestResult == null || hitTestResult.VisualHit == null) return false;

            return true;
        }

        public bool Enabled { get; set; }
        private bool CanDrag { get { return draggableItems != null && draggableItems.Count > 0; } }

        public bool IsDragging { get; private set; }

        private Point dragStartPoint;
        internal override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (CheckOverScrollBar(e.GetPosition(TreeView))) return;

            // initalize draggable items on click. Doing that in mouse move results in drag operations,
            // when the border is visible.
            draggableItems = GetDraggableItems(e.GetPosition(TreeView));

            if (CanDrag)
            {
                dragStartPoint = e.GetPosition(null);
            }
        }

        internal override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            // otherwise drops are triggered even if no node was selected in drop
            draggableItems = null;
        }

        internal override void OnMouseMove(MouseEventArgs e)
        {
            IsDragging = false;

            if (!IsLeftButtonDown || CheckOverScrollBar(e.GetPosition(TreeView)))
            {
                CleanUpAdorners();
                return;
            }

            if (!CanDrag) return;

            //drag actually starting?
            Point mousePos = e.GetPosition(null);
            Vector diff = dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed
                && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {

                IsDragging = true;

                DragContent dragData = new DragContent();
                foreach (var item in draggableItems)
                {
                    DragParameters dragParameters = new DragParameters(item);
                    TreeView.DragCommand.Execute(dragParameters);
                    dragData.Add(dragParameters.DraggedObject);
                }

                DragStart(dragData);
                DragDo(dragData);
                DragEnd();
                e.Handled = true;
            }
        }

        private void CleanUpAdorners()
        {
            if (insertAdorner != null)
            {
                insertAdorner.Dispose();
                insertAdorner = null;
            }
        }

        /// <summary>
        /// Scrolls if mouse is pressed and over scroll border. 
        /// </summary>
        /// <param name="position">Mouse position relative to treeView control.</param>
        /// <returns>Returns true if over scroll border, otherwise false.</returns>
        internal bool TryScroll(Point position)
        {
            if (!IsLeftButtonDown) return false;

            double scrollDelta;
            if (position.Y < AutoScroller.scrollBorderSize)
            {
                //scroll down
                scrollDelta = -AutoScroller.scrollDelta;
            }
            else if ((TreeView.RenderSize.Height - position.Y) < AutoScroller.scrollBorderSize)
            {
                //scroll up
                scrollDelta = AutoScroller.scrollDelta;
            }
            else
            {
                stopWatch = null;
                return false;
            }

            if (stopWatch == null || stopWatch.ElapsedMilliseconds > AutoScroller.scrollDelay)
            {
                autoScroller.Scroll(scrollDelta);
                stopWatch = new Stopwatch();
                stopWatch.Start();
            }

            return true;
        }

        private void DragDo(DragContent dragData)
        {
            DragDrop.DoDragDrop(TreeView, new DataObject(dragData), DragDropEffects.All);
        }

        private void DragEnd()
        {   
            autoScroller.IsEnabled = false;

            // Remove the drag adorner from the adorner layer.
            CleanUpAdorners();

            if (insertAdorner != null)
            {
                insertAdorner.Dispose();
                insertAdorner = null;
            }

            if (itemMouseIsOver != null)
            {
                itemMouseIsOver.IsCurrentDropTarget = false;
                itemMouseIsOver = null;
            }
        }

        private void DragStart(DragContent dragData)
        {
            autoScroller.IsEnabled = true;
        }

        private CanInsertReturn CanInsert(TreeViewExItem item, Func<UIElement, Point> getPositionDelegate, IDataObject data)
        {
            if (TreeView.DropCommand == null) return null;

            if (item == null)
            {
                return null;
            }
            else
            {
                // get position over element
                Size size = item.RenderSize;
                Point positionRelativeToItem = getPositionDelegate(item);

                // decide whether to insert before or after item
                bool after = true;
                if (positionRelativeToItem.Y > dragAreaSize)
                {
                    if (size.Height - positionRelativeToItem.Y > dragAreaSize)
                    {
                        return null;
                    }
                }
                else
                {
                    after = false;
                }

                // get index, where to insert                
                TreeViewExItem parentItem = item.ParentTreeViewItem;
                ItemContainerGenerator itemContainerGenerator = (parentItem != null)?parentItem.ItemContainerGenerator:TreeView.ItemContainerGenerator;
                int index = itemContainerGenerator.IndexFromContainer(item);                
                if (after)
                {
                    // dont allow insertion after item, if item has children
                    if (item.HasItems) return null;
                    index++;
                }               

                // ask if insertion is allowed
                if (TreeView.DropCommand.CanExecute(new DropParameters(parentItem, data, index)))
                {
                    return new CanInsertReturn("", index, !after);
                }
            }

            return null;
        }
        
        private bool CanDrop(TreeViewExItem item, IDataObject data)
        {
            if (TreeView.DropCommand == null) return false;
            
            return TreeView.DropCommand.CanExecute(new DropParameters(item, data));
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            TreeViewExItem item = GetTreeViewItemUnderMouse(e.GetPosition(TreeView));
            //if (item == null)
            //{
            //    CleanUpAdorners();
            //    return;
            //}

            CanInsertReturn canInsertReturn = CanInsert(item, e.GetPosition, e.Data);
            if (canInsertReturn != null)
            {
                // insert and return
                TreeView.DropCommand.Execute(new DropParameters(item.ParentTreeViewItem, e.Data, canInsertReturn.Index));
                CleanUpAdorners();
                return;
            }

            // check if drop is possible
            if (CanDrop(item, e.Data))
            {
                // drop and return
                TreeView.DropCommand.Execute(new DropParameters(item, e.Data));
            }

            CleanUpAdorners();
        }

        TreeViewExItem itemMouseIsOver;
        void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            // drag over is the only event which returns the position
            Point point = e.GetPosition(TreeView);

            if (TryScroll(point)) return;

            if (IsMouseOverAdorner(point)) return;
            var itemsPresenter = TreeView.ScrollViewer.Content as ItemsPresenter;
            /*
            if (itemsPresenter.InputHitTest(e.GetPosition(itemsPresenter)) == null)
            {
                if (insertAdorner != null) insertAdorner.Dispose();
                return;
            }
            */

            if (itemMouseIsOver != null)
            {
                itemMouseIsOver.IsCurrentDropTarget = false;
            }

            itemMouseIsOver = GetTreeViewItemUnderMouse(point);
            //if (itemMouseIsOver == null) return;
            CanInsertReturn canInsertReturn = CanInsert(itemMouseIsOver, e.GetPosition, e.Data);
            if (canInsertReturn != null)
            {
                e.Effects = DragDropEffects.Move;

                if (insertAdorner == null)
                {
                    insertAdorner = new InsertAdorner(itemMouseIsOver, new InsertContent { Before = canInsertReturn.Before });
                }
                else
                {
                    insertAdorner.Dispose();
                    insertAdorner = new InsertAdorner(itemMouseIsOver, new InsertContent { Before = canInsertReturn.Before });
                }

                itemMouseIsOver.IsCurrentDropTarget = false;
            }
            else
            {
                if (insertAdorner != null)
                {
                    insertAdorner.Dispose();
                    insertAdorner = null;
                }

                if(CanDrop(itemMouseIsOver, e.Data))
                    e.Effects = DragDropEffects.Move;
                if (itemMouseIsOver != null)
                {
                    itemMouseIsOver.IsCurrentDropTarget = true;
                }
            }
        }

        private bool CheckOverScrollBar(Point positionRelativeToTree)
        {
            if (TreeView.ScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
                && positionRelativeToTree.X > TreeView.RenderSize.Width - SystemParameters.ScrollWidth)
            {
                return true;
            }

            if (TreeView.ScrollViewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible
                && positionRelativeToTree.Y > TreeView.RenderSize.Height - SystemParameters.ScrollHeight)
            {
                return true;
            }
          
            return false;
        }

        private List<TreeViewExItem> GetDraggableItems(Point mousePositionRelativeToTree)
        {
            if (TreeView.DragCommand == null) return new List<TreeViewExItem>();
            
            List<TreeViewExItem> items = TreeView.GetTreeViewItemsFor(TreeView.SelectedItems)
                .Where(item => TreeView.DragCommand.CanExecute(new DragParameters(item))).ToList();

            TreeViewExItem itemUnderMouse = GetTreeViewItemUnderMouse(mousePositionRelativeToTree);
            if(itemUnderMouse == null) return new List<TreeViewExItem>();
                
            if (items.Contains(itemUnderMouse))
            {
                return items;
            }

            //mouse is not over an selected item. We have to check if it is over the content. In this case we have to select and start drag n drop.
            var contentPresenter = itemUnderMouse.Template.FindName("content", itemUnderMouse) as ContentPresenter;
            if (contentPresenter.IsMouseOver)
            {
                if(TreeView.DragCommand.CanExecute(new DragParameters(itemUnderMouse)))
                    return new List<TreeViewExItem> { itemUnderMouse };
            }

            return new List<TreeViewExItem>();
        }

        public void Dispose()
        {
            if (TreeView != null)
            {
                TreeView.Drop -= OnDrop;
                TreeView.DragOver -= OnDragOver;
                TreeView.DragLeave -= OnDragLeave;
            }

            if (itemMouseIsOver != null)
            {
                itemMouseIsOver.IsCurrentDropTarget = false;
                itemMouseIsOver = null;
            }
        }
    }
}
