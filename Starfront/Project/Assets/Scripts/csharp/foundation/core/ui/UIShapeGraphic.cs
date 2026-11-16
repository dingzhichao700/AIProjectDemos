using UnityEngine;
using UnityEngine.UI;

/// <summary>由界面结构数据还原凸多边形或折线；不生成额外位图素材。</summary>
public sealed class UIShapeGraphic : MaskableGraphic {
    public Vector2[] points;
    public bool openLine;
    public Color stroke = Color.clear;
    public float lineWidth = 1;

    protected override void OnPopulateMesh(VertexHelper vertices) {
        vertices.Clear();
        if (points == null || points.Length < 2) return;
        Vector2 offset = new Vector2(-rectTransform.pivot.x * rectTransform.rect.width, (1-rectTransform.pivot.y) * rectTransform.rect.height);
        if (!openLine) {
            for (int i=0;i<points.Length;i++) vertices.AddVert(offset + new Vector2(points[i].x,-points[i].y),color,Vector2.zero);
            for (int i=1;i<points.Length-1;i++) vertices.AddTriangle(0,i,i+1);
        }
        Color lineColor = openLine ? color : stroke;
        if (lineColor.a <= 0 || lineWidth <= 0) return;
        int segments = openLine ? points.Length-1 : points.Length;
        for (int i=0;i<segments;i++) {
            Vector2 a=offset+new Vector2(points[i].x,-points[i].y), b=offset+new Vector2(points[(i+1)%points.Length].x,-points[(i+1)%points.Length].y);
            Vector2 direction=b-a;
            if (direction.sqrMagnitude < .0001f) continue;
            Vector2 normal=new Vector2(-direction.y,direction.x).normalized * lineWidth*.5f;
            int start=vertices.currentVertCount;
            vertices.AddVert(a+normal,lineColor,Vector2.zero);vertices.AddVert(b+normal,lineColor,Vector2.zero);
            vertices.AddVert(b-normal,lineColor,Vector2.zero);vertices.AddVert(a-normal,lineColor,Vector2.zero);
            vertices.AddTriangle(start,start+1,start+2);vertices.AddTriangle(start,start+2,start+3);
        }
    }
}
