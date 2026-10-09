using System.Collections.Generic;
public class AOTGenericReferences : UnityEngine.MonoBehaviour
{

	// {{ AOT assemblies
	public static readonly IReadOnlyList<string> PatchedAOTAssemblyList = new List<string>
	{
		"DOTween.dll",
		"Foundation.dll",
		"Luban.Runtime.dll",
		"Newtonsoft.Json.dll",
		"System.Core.dll",
		"UnityEngine.CoreModule.dll",
		"mscorlib.dll",
	};
	// }}

	// {{ constraint implement type
	// }} 

	// {{ AOT generic types
	// Handler.<>c__12<int>
	// Handler.<>c__12<object>
	// Handler.<>c__13<object,int>
	// Handler.<>c__13<object,object>
	// Handler.<>c__14<object,object,int>
	// System.Action<BulletLaunchVO>
	// System.Action<ResLoadInfo>
	// System.Action<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Action<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Action<UnityEngine.Vector2,object>
	// System.Action<UnityEngine.Vector2>
	// System.Action<byte>
	// System.Action<cfg.vector2>
	// System.Action<float>
	// System.Action<int,object,object>
	// System.Action<int,object>
	// System.Action<int>
	// System.Action<long>
	// System.Action<object,UnityEngine.Vector2>
	// System.Action<object,byte>
	// System.Action<object,int,object>
	// System.Action<object,int>
	// System.Action<object,object,UnityEngine.Vector2>
	// System.Action<object,object,int>
	// System.Action<object,object>
	// System.Action<object>
	// System.Collections.Generic.ArraySortHelper<ResLoadInfo>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ArraySortHelper<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ArraySortHelper<UnityEngine.Vector2>
	// System.Collections.Generic.ArraySortHelper<cfg.vector2>
	// System.Collections.Generic.ArraySortHelper<float>
	// System.Collections.Generic.ArraySortHelper<int>
	// System.Collections.Generic.ArraySortHelper<long>
	// System.Collections.Generic.ArraySortHelper<object>
	// System.Collections.Generic.Comparer<ResLoadInfo>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.Comparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.Comparer<UnityEngine.Vector2>
	// System.Collections.Generic.Comparer<cfg.vector2>
	// System.Collections.Generic.Comparer<float>
	// System.Collections.Generic.Comparer<int>
	// System.Collections.Generic.Comparer<long>
	// System.Collections.Generic.Comparer<object>
	// System.Collections.Generic.Dictionary.Enumerator<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.KeyCollection<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary.KeyCollection<int,int>
	// System.Collections.Generic.Dictionary.KeyCollection<int,object>
	// System.Collections.Generic.Dictionary.KeyCollection<long,object>
	// System.Collections.Generic.Dictionary.KeyCollection<object,float>
	// System.Collections.Generic.Dictionary.KeyCollection<object,int>
	// System.Collections.Generic.Dictionary.KeyCollection<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,float>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection.Enumerator<object,object>
	// System.Collections.Generic.Dictionary.ValueCollection<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary.ValueCollection<int,int>
	// System.Collections.Generic.Dictionary.ValueCollection<int,object>
	// System.Collections.Generic.Dictionary.ValueCollection<long,object>
	// System.Collections.Generic.Dictionary.ValueCollection<object,float>
	// System.Collections.Generic.Dictionary.ValueCollection<object,int>
	// System.Collections.Generic.Dictionary.ValueCollection<object,object>
	// System.Collections.Generic.Dictionary<int,BulletLauncherModifierVO>
	// System.Collections.Generic.Dictionary<int,int>
	// System.Collections.Generic.Dictionary<int,object>
	// System.Collections.Generic.Dictionary<long,object>
	// System.Collections.Generic.Dictionary<object,float>
	// System.Collections.Generic.Dictionary<object,int>
	// System.Collections.Generic.Dictionary<object,object>
	// System.Collections.Generic.EqualityComparer<BulletLauncherModifierVO>
	// System.Collections.Generic.EqualityComparer<float>
	// System.Collections.Generic.EqualityComparer<int>
	// System.Collections.Generic.EqualityComparer<long>
	// System.Collections.Generic.EqualityComparer<object>
	// System.Collections.Generic.HashSet.Enumerator<int>
	// System.Collections.Generic.HashSet.Enumerator<object>
	// System.Collections.Generic.HashSet<int>
	// System.Collections.Generic.HashSet<object>
	// System.Collections.Generic.HashSetEqualityComparer<int>
	// System.Collections.Generic.HashSetEqualityComparer<object>
	// System.Collections.Generic.ICollection<ResLoadInfo>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,BulletLauncherModifierVO>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ICollection<UnityEngine.Vector2>
	// System.Collections.Generic.ICollection<cfg.vector2>
	// System.Collections.Generic.ICollection<float>
	// System.Collections.Generic.ICollection<int>
	// System.Collections.Generic.ICollection<long>
	// System.Collections.Generic.ICollection<object>
	// System.Collections.Generic.IComparer<ResLoadInfo>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IComparer<UnityEngine.Vector2>
	// System.Collections.Generic.IComparer<cfg.vector2>
	// System.Collections.Generic.IComparer<float>
	// System.Collections.Generic.IComparer<int>
	// System.Collections.Generic.IComparer<long>
	// System.Collections.Generic.IComparer<object>
	// System.Collections.Generic.IDictionary<int,int>
	// System.Collections.Generic.IDictionary<object,float>
	// System.Collections.Generic.IDictionary<object,int>
	// System.Collections.Generic.IDictionary<object,object>
	// System.Collections.Generic.IEnumerable<ResLoadInfo>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,BulletLauncherModifierVO>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerable<UnityEngine.Vector2>
	// System.Collections.Generic.IEnumerable<cfg.vector2>
	// System.Collections.Generic.IEnumerable<float>
	// System.Collections.Generic.IEnumerable<int>
	// System.Collections.Generic.IEnumerable<long>
	// System.Collections.Generic.IEnumerable<object>
	// System.Collections.Generic.IEnumerator<ResLoadInfo>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,BulletLauncherModifierVO>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<int,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<long,object>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IEnumerator<UnityEngine.Vector2>
	// System.Collections.Generic.IEnumerator<cfg.vector2>
	// System.Collections.Generic.IEnumerator<float>
	// System.Collections.Generic.IEnumerator<int>
	// System.Collections.Generic.IEnumerator<long>
	// System.Collections.Generic.IEnumerator<object>
	// System.Collections.Generic.IEqualityComparer<int>
	// System.Collections.Generic.IEqualityComparer<long>
	// System.Collections.Generic.IEqualityComparer<object>
	// System.Collections.Generic.IList<ResLoadInfo>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.IList<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.IList<UnityEngine.Vector2>
	// System.Collections.Generic.IList<cfg.vector2>
	// System.Collections.Generic.IList<float>
	// System.Collections.Generic.IList<int>
	// System.Collections.Generic.IList<long>
	// System.Collections.Generic.IList<object>
	// System.Collections.Generic.IReadOnlyCollection<int>
	// System.Collections.Generic.IReadOnlyCollection<object>
	// System.Collections.Generic.IReadOnlyDictionary<int,int>
	// System.Collections.Generic.IReadOnlyList<UnityEngine.Vector2>
	// System.Collections.Generic.IReadOnlyList<int>
	// System.Collections.Generic.IReadOnlyList<object>
	// System.Collections.Generic.KeyValuePair<int,BulletLauncherModifierVO>
	// System.Collections.Generic.KeyValuePair<int,int>
	// System.Collections.Generic.KeyValuePair<int,object>
	// System.Collections.Generic.KeyValuePair<long,object>
	// System.Collections.Generic.KeyValuePair<object,float>
	// System.Collections.Generic.KeyValuePair<object,int>
	// System.Collections.Generic.KeyValuePair<object,object>
	// System.Collections.Generic.List.Enumerator<ResLoadInfo>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List.Enumerator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.List.Enumerator<UnityEngine.Vector2>
	// System.Collections.Generic.List.Enumerator<cfg.vector2>
	// System.Collections.Generic.List.Enumerator<float>
	// System.Collections.Generic.List.Enumerator<int>
	// System.Collections.Generic.List.Enumerator<long>
	// System.Collections.Generic.List.Enumerator<object>
	// System.Collections.Generic.List<ResLoadInfo>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.List<UnityEngine.Vector2>
	// System.Collections.Generic.List<cfg.vector2>
	// System.Collections.Generic.List<float>
	// System.Collections.Generic.List<int>
	// System.Collections.Generic.List<long>
	// System.Collections.Generic.List<object>
	// System.Collections.Generic.ObjectComparer<ResLoadInfo>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.Generic.ObjectComparer<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.Generic.ObjectComparer<UnityEngine.Vector2>
	// System.Collections.Generic.ObjectComparer<cfg.vector2>
	// System.Collections.Generic.ObjectComparer<float>
	// System.Collections.Generic.ObjectComparer<int>
	// System.Collections.Generic.ObjectComparer<long>
	// System.Collections.Generic.ObjectComparer<object>
	// System.Collections.Generic.ObjectEqualityComparer<BulletLauncherModifierVO>
	// System.Collections.Generic.ObjectEqualityComparer<float>
	// System.Collections.Generic.ObjectEqualityComparer<int>
	// System.Collections.Generic.ObjectEqualityComparer<long>
	// System.Collections.Generic.ObjectEqualityComparer<object>
	// System.Collections.Generic.Queue.Enumerator<RecentDamageTracker.DamageRecord>
	// System.Collections.Generic.Queue<RecentDamageTracker.DamageRecord>
	// System.Collections.Generic.Stack.Enumerator<object>
	// System.Collections.Generic.Stack<object>
	// System.Collections.ObjectModel.ReadOnlyCollection<ResLoadInfo>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Collections.ObjectModel.ReadOnlyCollection<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector2>
	// System.Collections.ObjectModel.ReadOnlyCollection<cfg.vector2>
	// System.Collections.ObjectModel.ReadOnlyCollection<float>
	// System.Collections.ObjectModel.ReadOnlyCollection<int>
	// System.Collections.ObjectModel.ReadOnlyCollection<long>
	// System.Collections.ObjectModel.ReadOnlyCollection<object>
	// System.Collections.ObjectModel.ReadOnlyDictionary.DictionaryEnumerator<int,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.DictionaryEnumerator<object,float>
	// System.Collections.ObjectModel.ReadOnlyDictionary.DictionaryEnumerator<object,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.DictionaryEnumerator<object,object>
	// System.Collections.ObjectModel.ReadOnlyDictionary.KeyCollection<int,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.KeyCollection<object,float>
	// System.Collections.ObjectModel.ReadOnlyDictionary.KeyCollection<object,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.KeyCollection<object,object>
	// System.Collections.ObjectModel.ReadOnlyDictionary.ValueCollection<int,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.ValueCollection<object,float>
	// System.Collections.ObjectModel.ReadOnlyDictionary.ValueCollection<object,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary.ValueCollection<object,object>
	// System.Collections.ObjectModel.ReadOnlyDictionary<int,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary<object,float>
	// System.Collections.ObjectModel.ReadOnlyDictionary<object,int>
	// System.Collections.ObjectModel.ReadOnlyDictionary<object,object>
	// System.Comparison<ResLoadInfo>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Comparison<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Comparison<UnityEngine.Vector2>
	// System.Comparison<cfg.vector2>
	// System.Comparison<float>
	// System.Comparison<int>
	// System.Comparison<long>
	// System.Comparison<object>
	// System.Func<System.Collections.Generic.KeyValuePair<object,float>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<object,float>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Func<System.Collections.Generic.KeyValuePair<object,object>,byte>
	// System.Func<System.Collections.Generic.KeyValuePair<object,object>,object>
	// System.Func<System.Threading.Tasks.VoidTaskResult>
	// System.Func<UnityEngine.Vector2,object>
	// System.Func<UnityEngine.Vector2>
	// System.Func<float,byte>
	// System.Func<float,float,float>
	// System.Func<float,object>
	// System.Func<int,byte>
	// System.Func<int,int,int,object>
	// System.Func<int,object>
	// System.Func<long>
	// System.Func<object,System.Threading.Tasks.VoidTaskResult>
	// System.Func<object,byte,byte>
	// System.Func<object,byte>
	// System.Func<object,float>
	// System.Func<object,int,UnityEngine.Vector2,object>
	// System.Func<object,int>
	// System.Func<object,object,object>
	// System.Func<object,object>
	// System.Func<object>
	// System.Linq.Enumerable.<CastIterator>d__99<int>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Linq.Enumerable.Iterator<float>
	// System.Linq.Enumerable.Iterator<int>
	// System.Linq.Enumerable.Iterator<object>
	// System.Linq.Enumerable.WhereEnumerableIterator<object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,float>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<System.Collections.Generic.KeyValuePair<object,object>,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<float,object>
	// System.Linq.Enumerable.WhereSelectArrayIterator<int,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,float>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<System.Collections.Generic.KeyValuePair<object,object>,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<float,object>
	// System.Linq.Enumerable.WhereSelectEnumerableIterator<int,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,float>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,int>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<System.Collections.Generic.KeyValuePair<object,object>,object>
	// System.Linq.Enumerable.WhereSelectListIterator<float,object>
	// System.Linq.Enumerable.WhereSelectListIterator<int,object>
	// System.Nullable<UnityEngine.Vector2>
	// System.Nullable<int>
	// System.Predicate<ResLoadInfo>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,float>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,int>>
	// System.Predicate<System.Collections.Generic.KeyValuePair<object,object>>
	// System.Predicate<UnityEngine.Vector2>
	// System.Predicate<cfg.vector2>
	// System.Predicate<float>
	// System.Predicate<int>
	// System.Predicate<long>
	// System.Predicate<object>
	// System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter<System.Threading.Tasks.VoidTaskResult>
	// System.Runtime.CompilerServices.ConfiguredTaskAwaitable<System.Threading.Tasks.VoidTaskResult>
	// System.Runtime.CompilerServices.TaskAwaiter<System.Threading.Tasks.VoidTaskResult>
	// System.Threading.Tasks.ContinuationTaskFromResultTask<System.Threading.Tasks.VoidTaskResult>
	// System.Threading.Tasks.Task<System.Threading.Tasks.VoidTaskResult>
	// System.Threading.Tasks.TaskFactory.<>c__DisplayClass35_0<System.Threading.Tasks.VoidTaskResult>
	// System.Threading.Tasks.TaskFactory<System.Threading.Tasks.VoidTaskResult>
	// UnityEngine.Events.UnityAction<object>
	// }}

	public void RefMethods()
	{
		// object DG.Tweening.TweenSettingsExtensions.OnComplete<object>(object,DG.Tweening.TweenCallback)
		// object DG.Tweening.TweenSettingsExtensions.SetDelay<object>(object,float)
		// object DG.Tweening.TweenSettingsExtensions.SetEase<object>(object,DG.Tweening.Ease)
		// System.Void EventDispatcher.Dispatch<int,object,object>(string,int,object,object)
		// System.Void EventDispatcher.Dispatch<int,object>(string,int,object)
		// System.Void EventDispatcher.Dispatch<int>(string,int)
		// System.Void EventDispatcher.Off<int,object,object>(string,System.Action<int,object,object>)
		// System.Void EventDispatcher.Off<int,object>(string,System.Action<int,object>)
		// System.Void EventDispatcher.Off<int>(string,System.Action<int>)
		// System.Void EventDispatcher.On<int,object,object>(string,System.Action<int,object,object>)
		// System.Void EventDispatcher.On<int,object>(string,System.Action<int,object>)
		// System.Void EventDispatcher.On<int>(string,System.Action<int>)
		// Handler Handler.Create<int>(object,System.Action<int>,int)
		// Handler Handler.Create<object,int>(object,System.Action<object,int>,object,int)
		// Handler Handler.Create<object>(object,System.Action<object>,object)
		// System.Void Handler.SetCallback<int>(System.Action<int>,int)
		// System.Void Handler.SetCallback<object,int>(System.Action<object,int>,object,int)
		// System.Void Handler.SetCallback<object,object,int>(System.Action<object,object,int>,object,object,int)
		// System.Void Handler.SetCallback<object,object>(System.Action<object,object>,object,object)
		// System.Void Handler.SetCallback<object>(System.Action<object>,object)
		// object JsonFileUtil.Load<object>(string)
		// System.Void JsonFileUtil.Save<object>(string,object)
		// string Luban.StringUtil.CollectionToString<cfg.vector2>(System.Collections.Generic.IEnumerable<cfg.vector2>)
		// string Luban.StringUtil.CollectionToString<int>(System.Collections.Generic.IEnumerable<int>)
		// string Luban.StringUtil.CollectionToString<object>(System.Collections.Generic.IEnumerable<object>)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string)
		// object Newtonsoft.Json.JsonConvert.DeserializeObject<object>(string,Newtonsoft.Json.JsonSerializerSettings)
		// object Newtonsoft.Json.Linq.Extensions.Convert<object,object>(object)
		// object Newtonsoft.Json.Linq.JToken.Value<object>(object)
		// object System.Activator.CreateInstance<object>()
		// System.Collections.ObjectModel.ReadOnlyCollection<UnityEngine.Vector2> System.Array.AsReadOnly<UnityEngine.Vector2>(UnityEngine.Vector2[])
		// System.Collections.ObjectModel.ReadOnlyCollection<int> System.Array.AsReadOnly<int>(int[])
		// System.Collections.ObjectModel.ReadOnlyCollection<object> System.Array.AsReadOnly<object>(object[])
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.Cast<int>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<int> System.Linq.Enumerable.CastIterator<int>(System.Collections.IEnumerable)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,float>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,float>>,System.Func<System.Collections.Generic.KeyValuePair<object,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,int>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,int>>,System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<System.Collections.Generic.KeyValuePair<object,object>,object>(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<object,object>>,System.Func<System.Collections.Generic.KeyValuePair<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<float,object>(System.Collections.Generic.IEnumerable<float>,System.Func<float,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Select<int,object>(System.Collections.Generic.IEnumerable<int>,System.Func<int,object>)
		// System.Collections.Generic.List<int> System.Linq.Enumerable.ToList<int>(System.Collections.Generic.IEnumerable<int>)
		// System.Collections.Generic.List<object> System.Linq.Enumerable.ToList<object>(System.Collections.Generic.IEnumerable<object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,float>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,float>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,int>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,int>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<System.Collections.Generic.KeyValuePair<object,object>>.Select<object>(System.Func<System.Collections.Generic.KeyValuePair<object,object>,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<float>.Select<object>(System.Func<float,object>)
		// System.Collections.Generic.IEnumerable<object> System.Linq.Enumerable.Iterator<int>.Select<object>(System.Func<int,object>)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CfgManager.<EnsureLoadedAsync>d__2>(System.Runtime.CompilerServices.TaskAwaiter&,CfgManager.<EnsureLoadedAsync>d__2&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CfgManager.<LoadAllAsync>d__3>(System.Runtime.CompilerServices.TaskAwaiter&,CfgManager.<LoadAllAsync>d__3&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_2>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_2>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_3>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_3>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_4>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_4>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CfgManager.<EnsureLoadedAsync>d__2>(System.Runtime.CompilerServices.TaskAwaiter&,CfgManager.<EnsureLoadedAsync>d__2&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,CfgManager.<LoadAllAsync>d__3>(System.Runtime.CompilerServices.TaskAwaiter&,CfgManager.<LoadAllAsync>d__3&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_2>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_2>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_3>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_3>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c.<<Enter>b__1_4>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c.<<Enter>b__1_4>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Threading.Tasks.VoidTaskResult>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d>(System.Runtime.CompilerServices.TaskAwaiter&,MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<CfgManager.<EnsureLoadedAsync>d__2>(CfgManager.<EnsureLoadedAsync>d__2&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<CfgManager.<LoadAllAsync>d__3>(CfgManager.<LoadAllAsync>d__3&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<MainContentLoader.<>c.<<Enter>b__1_2>d>(MainContentLoader.<>c.<<Enter>b__1_2>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<MainContentLoader.<>c.<<Enter>b__1_3>d>(MainContentLoader.<>c.<<Enter>b__1_3>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<MainContentLoader.<>c.<<Enter>b__1_4>d>(MainContentLoader.<>c.<<Enter>b__1_4>d&)
		// System.Void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d>(MainContentLoader.<>c__DisplayClass1_1.<<Enter>g__LoadModuleAsync|5>d&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,LoadingControl.<OpenRequest>d__5>(System.Runtime.CompilerServices.TaskAwaiter&,LoadingControl.<OpenRequest>d__5&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,LoadingPanel.<LoadResources>d__17>(System.Runtime.CompilerServices.TaskAwaiter&,LoadingPanel.<LoadResources>d__17&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,LoginPanel.<LoadConfigs>d__13>(System.Runtime.CompilerServices.TaskAwaiter&,LoginPanel.<LoadConfigs>d__13&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,OpeningPanel.<LoadLoginResource>d__12>(System.Runtime.CompilerServices.TaskAwaiter&,OpeningPanel.<LoadLoginResource>d__12&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter,OpeningPanel.<LoadSelfResource>d__9>(System.Runtime.CompilerServices.TaskAwaiter&,OpeningPanel.<LoadSelfResource>d__9&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<LoadingControl.<OpenRequest>d__5>(LoadingControl.<OpenRequest>d__5&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<LoadingPanel.<LoadResources>d__17>(LoadingPanel.<LoadResources>d__17&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<LoginPanel.<LoadConfigs>d__13>(LoginPanel.<LoadConfigs>d__13&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<OpeningPanel.<LoadLoginResource>d__12>(OpeningPanel.<LoadLoginResource>d__12&)
		// System.Void System.Runtime.CompilerServices.AsyncVoidMethodBuilder.Start<OpeningPanel.<LoadSelfResource>d__9>(OpeningPanel.<LoadSelfResource>d__9&)
		// object& System.Runtime.CompilerServices.Unsafe.As<object,object>(object&)
		// System.Void* System.Runtime.CompilerServices.Unsafe.AsPointer<object>(object&)
		// TimeHandler TimeHandler.Create<object,object,int>(object,System.Action<object,object,int>,object,object,int)
		// TimeHandler TimeHandler.Create<object,object>(object,System.Action<object,object>,object,object)
		// TimeHandler TimeHandler.Create<object>(object,System.Action<object>,object)
		// System.Void Timer.Clear<object,object>(object,System.Action<object,object>)
		// System.Void Timer.Clear<object>(object,System.Action<object>)
		// System.Void Timer.Once<object,object,int>(object,int,System.Action<object,object,int>,object,object,int,bool)
		// System.Void Timer.Once<object,object>(object,int,System.Action<object,object>,object,object,bool)
		// System.Void Timer.Once<object>(object,int,System.Action<object>,object,bool)
		// object UnityEngine.Component.GetComponent<object>()
		// object UnityEngine.Component.GetComponentInParent<object>()
		// object UnityEngine.GameObject.AddComponent<object>()
		// object UnityEngine.GameObject.GetComponent<object>()
		// object UnityEngine.Object.Instantiate<object>(object,UnityEngine.Transform,bool)
		// string string.Join<cfg.vector2>(string,System.Collections.Generic.IEnumerable<cfg.vector2>)
		// string string.Join<int>(string,System.Collections.Generic.IEnumerable<int>)
		// string string.Join<object>(string,System.Collections.Generic.IEnumerable<object>)
		// string string.JoinCore<cfg.vector2>(System.Char*,int,System.Collections.Generic.IEnumerable<cfg.vector2>)
		// string string.JoinCore<int>(System.Char*,int,System.Collections.Generic.IEnumerable<int>)
		// string string.JoinCore<object>(System.Char*,int,System.Collections.Generic.IEnumerable<object>)
	}
}