

using System;
/**
* IDynamicScrollViewItem.cs
* 2019.04.22 調整結構,增加要移除自身(Scrollview Item)時,不須由Scrollview處理,直接執行Action即可.
* @author mosframe / https://github.com/mosframe
* 
*/
namespace Mosframe {

    /// <summary>
    /// DynamicScrollView Item interface
    /// </summary>
    public interface IDynamicScrollViewItem {
        /// <summary>
        /// ScrollView拖曳時，ScrollVie的Item須執行的func.
        /// </summary>
        /// <param name="index">item在陣列裡的索引</param>
        /// <param name="data">傳入的類別參數,需自行再轉型</param>        
	    void onUpdateItem( int index, object data );        
    }


}
