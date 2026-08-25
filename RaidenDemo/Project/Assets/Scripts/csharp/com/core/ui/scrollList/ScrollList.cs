using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/*
 * 滑动列表组件
 日期：2025-7-29
 备注：借助UGUI的ScrollRect来实现的自定义滑动列表，只支持水平或垂直方向的滑动

 */
[Serializable]
public class ScrollList : BaseView, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler {

    /**ScrollRect组件*/
    private ScrollRect _scrollRect;
    private ScrollRect scrollRect {
        get {
            if (_scrollRect == null) {
                _scrollRect = GetComponent<ScrollRect>();
            }
            return _scrollRect;
        }
    }
    /**内容容器*/
    private RectTransform _boxContent;
    private RectTransform boxContent {
        get {
            if (_boxContent == null) {
                _boxContent = scrollRect.content;
            }
            return _boxContent;
        }
    }

    /**列表布局方向（只支持一种方向，1横向2纵向）*/
    private int layoutDir => scrollRect.horizontal ? 1 : 2;

    /**水平限定数量*/
    public int horiLimitNum;

    /**垂直限定数量*/
    public int vertLimitNum;

    /**水平分布间距*/
    public int horiGap;

    /**垂直分布间距*/
    public int vertGap;

    /**列表item模板*/
    public GameObject itemTemplate;

    /**列表item模板宽度*/
    private int itemWidth => (int)(itemTemplate.transform as RectTransform).sizeDelta.x;

    /**列表item模板高度*/
    private int itemHeight => (int)(itemTemplate.transform as RectTransform).sizeDelta.y;

    /**横向实际个数*/
    private int horiRealNum;

    /**纵向实际个数*/
    private int vertRealNum;

    /**列表item渲染接口*/
    private Action<int, object> rendererAction;

    /**列表item点击接口*/
    private Action<int> listClickAction;

    /**按下的坐标*/
    private Vector2 posDown;

    /**上一个有效滑动值*/
    private double lastSlideValue = -1;

    /**数据列表*/
    private List<object> _dataList = new List<object> { };

    /**当前实例化的go的map*/
    private ConcurrentDictionary<int, GameObject> cellMap;

    /**item池*/
    private List<GameObject> itemPool;

    void Awake() {
        //if(scrollRect.movementType != MovementType.Clamped) {//除拖动锁定的情况外，要监听拖动来触发列表可视区域的改变
        scrollRect.onValueChanged.AddListener(OnSlide);
        //}
        itemTemplate.SetActive(false);
        RecycleItemsFromPrefab();
    }

    /**先收回一遍item（可以把编辑状态下创建item利用起来）*/
    private void RecycleItemsFromPrefab() {
        for (int i = boxContent.childCount - 1; i >= 0; i--) {
            GameObject child = boxContent.GetChild(i).gameObject;
            //克隆的item回收到池
            if (child.name == itemTemplate.name + "(Clone)") {
                AddToPool(child);
                child.SetActive(false);
            }
        }
    }

    private void OnSlide(Vector2 vec) {
        //取布局方向上滑动值，精确到小数点后2位
        double sliderValue = Math.Round(isHorizontal ? vec.x : vec.y, 2);
        if (lastSlideValue == -1 || lastSlideValue != sliderValue) {
            //Debug.Log("发生有效滑动" + sliderValue);
            //和缓存值比较，以防频繁触发
            lastSlideValue = sliderValue;
            UpdateItems();
        }
    }

    public void OnPointerClick(PointerEventData data) {
        if (listClickAction != null) {
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, results);

            GameObject current = data.pointerCurrentRaycast.gameObject;
            for (int i = 0; i < results.Count; i++) {
                //Debug.Log("点击碰撞对象：" + results[i]);
                foreach (var key in cellMap.Keys) {
                    GameObject go = GetCell(key);
                    if (results[i].gameObject == go) {
                        listClickAction.Invoke(key);
                        break;
                    }
                }
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData) {
        //posDown = eventData.position;
    }

    public void OnDrag(PointerEventData eventData) {
        /*if (boxContent.rect.height < viewPort.rect.height) {
            return;
        }
        float pivotY = eventData.position.y - posDown.y;

        Vector3 pivotVec = Vector3.up * pivotY;
        boxContent.localPosition += pivotVec;
        if (boxContent.localPosition.y < 0) //上边缘出界
        {
            boxContent.localPosition = Vector3.zero;
        } else if (boxContent.localPosition.y > boxContent.rect.height - viewPort.rect.height) //下边缘出界
        {
            boxContent.localPosition = Vector3.up * (boxContent.rect.height - viewPort.rect.height);
        }
        posDown = eventData.position;
        UpdateItems();*/
    }

    public void OnEndDrag(PointerEventData eventData) {
    }

    private List<object> _arrayData = new List<object>();

    public List<object> Array {
        get { return _arrayData; }
        set { _arrayData = value; }
    }

    public void SetList(List<object> myList) {
        foreach (var item in myList) {
            // 处理列表中的每个元素
            Console.WriteLine(item);
        }
    }

    public IReadOnlyList<object> arraySource => _dataList;

    public IEnumerable array {
        set {
            _dataList.Clear();

            if (value != null) {
                foreach (var item in value) {
                    _dataList.Add(item);
                }
            }

            int[] horiAndVertNum = CalcuGenerateNum((transform as RectTransform).rect.size.x, (transform as RectTransform).rect.size.y, itemWidth, itemHeight, horiLimitNum, vertLimitNum, horiGap, vertGap, isHorizontal, arraySource.Count);
            horiRealNum = horiAndVertNum[0];
            vertRealNum = horiAndVertNum[1];
            //根据生成数量，设置content容器的尺寸
            float contentW = horiRealNum * (itemWidth + horiGap) - horiGap;
            float contentH = vertRealNum * (itemHeight + vertGap) - vertGap;
            boxContent.sizeDelta = new Vector2(contentW, contentH);

            //ClearAllItems();

            /**
             * 因为viewPort的布局通常是stretch到父容器边界，但组件初始化时它的宽高往往还是（0,0），此时显示区域计算必定不准确。
             * 所以在这里设置数据触发的更新这里，统一延迟一帧再Update。
             */
            //UpdateItems();
            RookieEngine.timer.CallLater(this, UpdateItems);
        }
    }

    /**匿名渲染函数*/
    public Action<int, object> rendererHandler {
        set => rendererAction = value;
    }

    /**点击交互函数*/
    public Action<int> clickHandler {
        set => listClickAction = value;
    }

    private void ClearAllItems() {
        if (cellMap != null) {
            foreach (var key in cellMap.Keys) {
                GameObject listItem = cellMap[key];
                ScrollListItem item = listItem.GetComponent<ScrollListItem>();
                GameObject.Destroy(cellMap[key]);
            }
            cellMap.Clear();
        }
    }

    /// <summary>
    /// 更新要显示的item
    /// </summary>
    private void UpdateItems() {
        if (arraySource == null) {
            return;
        }
        if (cellMap == null) {
            cellMap = new ConcurrentDictionary<int, GameObject>();
        }

        //需要创建的item索引
        List<int> needIndexs = CalcuIndexs();

        //先回收多余的item
        foreach (var item in cellMap) {
            if (needIndexs.IndexOf(item.Key) < 0) {
                GameObject listItem = cellMap[item.Key];
                cellMap.TryRemove(item.Key, out listItem);

                //回收到池之前，先触发item的清理
                ScrollListItem scrollItemClass = listItem.GetComponent<ScrollListItem>();
                if (scrollItemClass) {
                    scrollItemClass.Clear();
                }
                AddToPool(listItem);
            }
        }
        //再创建缺少的item
        for (int i = 0; i < needIndexs.Count; i++) {
            int needIndex = needIndexs[i];
            if (!cellMap.ContainsKey(needIndex)) {
                GameObject item = GetItemFromPool();
                cellMap.TryAdd(needIndex, item);
                item.transform.SetParent(boxContent);
                item.transform.localScale = Vector3.one;
                int row = Mathf.FloorToInt(needIndex / horiRealNum);
                int column = needIndex % horiRealNum;
                //item.transform.localPosition = new Vector3((itemWidth + horiGap) * column, -(itemHeight + vertGap) * row, 0);
                (item.transform as RectTransform).anchoredPosition = new Vector3((itemWidth + horiGap) * column, -(itemHeight + vertGap) * row, 0);
            }
        }

        //再对要渲染的列表项进行：赋值或触发渲染函数
        foreach (int index in cellMap.Keys) {
            GameObject listItem = cellMap[index];
            ScrollListItem scrollItemClass = listItem.GetComponent<ScrollListItem>();
            if (scrollItemClass) {
                scrollItemClass.SetData(index, arraySource[index]);
            }
            if (rendererAction != null) {
                rendererAction.Invoke(index, arraySource[index]);
            }
        }
    }

    /// <summary>
    /// 根据当前视窗对应区域，计算对应的item索引
    /// </summary>
    /// <returns>对应的item索引</returns>
    private List<int> CalcuIndexs() {
        List<int> result = new List<int>();

        float beginX = isHorizontal ? -boxContent.localPosition.x : 0;
        float beginY = isHorizontal ? 0 : boxContent.localPosition.y;
        float endX = beginX + (transform as RectTransform).rect.width;
        float endY = beginY + (transform as RectTransform).rect.height;

        int beginColumn = Mathf.FloorToInt(beginX / (itemWidth + horiGap));
        int beginRow = Mathf.FloorToInt(beginY / (itemHeight + vertGap));
        int endColumn = Mathf.FloorToInt(endX / (itemWidth + horiGap));
        int endRow = Mathf.FloorToInt(endY / (itemHeight + vertGap));

        for (int row = beginRow; row <= endRow; row++) {
            for (int column = beginColumn; column <= endColumn; column++) {
                var index = GetIndexByRowAndColumn(row, column);
                if (index != -1) {
                    result.Add(index);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 计算理论上某排某列的对应的实际索引值
    /// </summary>
    /// <param name="row"></param>
    /// <param name="column"></param>
    /// <returns>索引值（-1表示无效）</returns>
    private int GetIndexByRowAndColumn(int row, int column) {
        if ((0 <= column && column < horiRealNum) && (0 <= row && row < vertRealNum)) {
            int index = horiRealNum * row + column;
            if (index < arraySource.Count) {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// 从池中取一个item实例
    /// </summary>
    /// <returns></returns>
    private GameObject GetItemFromPool() {
        GameObject item;
        if (itemPool != null && itemPool.Count > 0) {
            item = itemPool[0];
            itemPool.RemoveAt(0);
        } else {
            item = Instantiate(itemTemplate);
        }
        item.SetActive(true);
        return item;
    }

    /// <summary>
    /// 归还item实例到池中
    /// </summary>
    /// <param name="item"></param>
    private void AddToPool(GameObject item) {
        if (itemPool == null) {
            itemPool = new List<GameObject>();
        }
        item.SetActive(false);
        itemPool.Add(item);
    }

    /// <summary>
    /// 获取item
    /// </summary>
    /// <param name="index">索引</param>
    /// <returns></returns>
    public GameObject GetCell(int index) {
        if (cellMap.ContainsKey(index)) {
            return cellMap[index];
        }
        return null;
    }

    /**是否水平布局*/
    private bool isHorizontal => layoutDir == 1;

    public void Destory() {
        ClearAllItems();
        if (scrollRect != null) {
            scrollRect.onValueChanged.RemoveListener(OnSlide);
        }
        foreach (var key in cellMap.Keys) {
            GameObject go = GetCell(key);
            GameObject.Destroy(go);
        }
        rendererAction = null;
        listClickAction = null;
    }

    /// <summary>
    /// 计算列表生成item的横向和纵向的布局个数
    /// </summary>
    /// <param name="listW">列表宽度</param>
    /// <param name="listH">列表高度</param>
    /// <param name="itemW">item高度</param>
    /// <param name="itemH">item高度</param>
    /// <param name="horiLimitNum">指定横向个数（0为不限制）</param>
    /// <param name="vertLimitNum">指定纵向个数（0为不限制）</param>
    /// <param name="horiGap">横向间距</param>
    /// <param name="vertGap">纵向间距</param>
    /// <param name="isHorizontal">是否水平滑动</param>
    /// <param name="generateNum">生成数量</param>
    /// <returns>[横向个数，纵向个数]</returns>
    public static int[] CalcuGenerateNum(float listW, float listH, float itemW, float itemH, int horiLimitNum, int vertLimitNum, int horiGap, int vertGap, bool isHorizontal, int generateNum) {
        /**横向实际个数*/
        float horiRealNum;
        /**纵向实际个数*/
        float vertRealNum;
        if (vertLimitNum > 0) { //有指定纵向个数
            if (horiLimitNum > 0) {
                //有指定横向个数
                horiRealNum = Mathf.Min(horiLimitNum, generateNum);
                vertRealNum = Mathf.Min(vertLimitNum, Mathf.CeilToInt((float)generateNum / horiLimitNum));
            } else {
                //没有指定横向个数，根据列表长度算出来
                horiRealNum = Mathf.CeilToInt((float)generateNum / vertLimitNum);
                vertRealNum = Mathf.Min(vertLimitNum, generateNum);
            }
        } else if (horiLimitNum > 0) { //只指定横向个数
            horiRealNum = Mathf.Min(horiLimitNum, generateNum);
            vertRealNum = Mathf.CeilToInt((float)generateNum / horiLimitNum);
        } else {
            //都没指定个数，根据列表尺寸计算横纵的【最适个数】
            int suitableNumHori = Mathf.CeilToInt(listW / (itemW + horiGap));
            int suitableNumVert = Mathf.CeilToInt(listH / (itemH + vertGap));
            //再根据生成数量决定横纵的实际个数
            if (generateNum < suitableNumHori * suitableNumVert) {
                //生成数量小于横纵最适个数的容纳上限
                horiRealNum = Mathf.Min(generateNum, suitableNumHori);
                vertRealNum = Mathf.Min(Mathf.CeilToInt((float)generateNum / suitableNumHori), suitableNumVert);
            } else {
                //超出上限，根据滑动方向决定
                if (isHorizontal) {
                    //横向滑动
                    horiRealNum = Mathf.CeilToInt((float)generateNum / suitableNumVert); ;
                    vertRealNum = suitableNumVert;
                } else {
                    //纵向滑动
                    horiRealNum = suitableNumHori;
                    vertRealNum = Mathf.CeilToInt((float)generateNum / suitableNumHori);
                }
            }
        }
        return new int[] { (int)horiRealNum, (int)vertRealNum };
    }

}
