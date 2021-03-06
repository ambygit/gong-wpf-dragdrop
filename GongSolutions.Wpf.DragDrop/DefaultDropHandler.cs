﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using GongSolutions.Wpf.DragDrop.Utilities;
using System.Windows.Controls;

namespace GongSolutions.Wpf.DragDrop
{
  /// <summary>
  /// A default insertion drop handler for the most common usages
  /// </summary>
  public class DefaultDropHandler : IDropTarget
  {
    /// <summary>
    /// Updates the current drag state.
    /// </summary>
    /// <param name="dropInfo">Information about the drag.</param>
    /// <remarks>
    /// To allow a drop at the current drag position, the <see cref="DropInfo.Effects" /> property on
    /// <paramref name="dropInfo" /> should be set to a value other than <see cref="DragDropEffects.None" />
    /// and <see cref="DropInfo.Data" /> should be set to a non-null value.
    /// </remarks>
    public virtual void DragOver(IDropInfo dropInfo)
    {
      if (CanAcceptData(dropInfo)) {
        // when source is the same as the target set the move effect otherwise set the copy effect
        dropInfo.Effects = dropInfo.DragInfo.VisualSource == dropInfo.VisualTarget ? DragDropEffects.Move : DragDropEffects.Copy;
        dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
      }
    }

    /// <summary>
    /// Performs a drop.
    /// </summary>
    /// <param name="dropInfo">Information about the drop.</param>
    public virtual void Drop(IDropInfo dropInfo)
    {
      if (dropInfo == null || dropInfo.DragInfo == null) {
        return;
      }
      
      var insertIndex = dropInfo.InsertIndex != dropInfo.UnfilteredInsertIndex ? dropInfo.UnfilteredInsertIndex : dropInfo.InsertIndex;
      var destinationList = dropInfo.TargetCollection.TryGetList();
      var data = ExtractData(dropInfo.Data);

      // when source is the same as the target remove the data from source and fix the insertion index
      if (dropInfo.DragInfo.VisualSource == dropInfo.VisualTarget) {
        var sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();

        foreach (var o in data) {
          var index = sourceList.IndexOf(o);

          if (index != -1) {
            sourceList.RemoveAt(index);
            // so, is the source list the destination list too ?
            if (Equals(sourceList, destinationList) && index < insertIndex) {
              --insertIndex;
            }
          }
        }
      }

      // check for cloning
      var cloneData = dropInfo.Effects.HasFlag(DragDropEffects.Copy) || dropInfo.Effects.HasFlag(DragDropEffects.Link);
      foreach (var o in data) {
        var obj2Insert = o;
        if (cloneData) {
          var cloneable = o as ICloneable;
          if (cloneable != null) {
            obj2Insert = cloneable.Clone();
          }
        }
        destinationList.Insert(insertIndex++, obj2Insert);
      }
    }

    /// <summary>
    /// Test the specified drop information for the right data.
    /// </summary>
    /// <param name="dropInfo">The drop information.</param>
    public static bool CanAcceptData(IDropInfo dropInfo)
    {
      if (dropInfo == null || dropInfo.DragInfo == null) {
        return false;
      }

      if (!dropInfo.IsSameDragDropContextAsSource) {
        return false;
      }

      if (dropInfo.DragInfo.SourceCollection == dropInfo.TargetCollection) {
        return dropInfo.TargetCollection.TryGetList() != null;
      } else if (dropInfo.DragInfo.SourceCollection is ItemCollection) {
        return false;
      } else if (dropInfo.TargetCollection == null) {
        return false;
      } else {
        if (TestCompatibleTypes(dropInfo.TargetCollection, dropInfo.Data)) {
          return !IsChildOf(dropInfo.VisualTargetItem, dropInfo.DragInfo.VisualSourceItem);
        } else {
          return false;
        }
      }
    }

    public static IEnumerable ExtractData(object data)
    {
      if (data is IEnumerable && !(data is string)) {
        return (IEnumerable)data;
      } else {
        return Enumerable.Repeat(data, 1);
      }
    }

    protected static bool IsChildOf(UIElement targetItem, UIElement sourceItem)
    {
      var parent = ItemsControl.ItemsControlFromItemContainer(targetItem);

      while (parent != null) {
        if (parent == sourceItem) {
          return true;
        }

        parent = ItemsControl.ItemsControlFromItemContainer(parent);
      }

      return false;
    }

    protected static bool TestCompatibleTypes(IEnumerable target, object data)
    {
      TypeFilter filter = (t, o) => {
                            return (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                          };

      var enumerableInterfaces = target.GetType().FindInterfaces(filter, null);
      var enumerableTypes = from i in enumerableInterfaces select i.GetGenericArguments().Single();

      if (enumerableTypes.Count() > 0) {
        var dataType = TypeUtilities.GetCommonBaseClass(ExtractData(data));
        return enumerableTypes.Any(t => t.IsAssignableFrom(dataType));
      } else {
        return target is IList;
      }
    }
  }
}