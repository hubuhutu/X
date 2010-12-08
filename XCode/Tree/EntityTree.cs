﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web.UI.WebControls;
using System.Xml.Serialization;
using NewLife.Log;
using NewLife.Reflection;

namespace XCode
{
    /// <summary>
    /// 主键为整型的实体树基类
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class EntityTree<TEntity> : EntityTree<Int32, TEntity> where TEntity : EntityTree<TEntity>, new()
    { }

    /// <summary>
    /// 实体树基类，具有树形结构的实体继承该类即可得到各种树操作功能
    /// </summary>
    /// <typeparam name="TKey">主键类型</typeparam>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class EntityTree<TKey, TEntity> : Entity<TEntity>, IEntityTree<TEntity>, IEntityTreeExtend<TEntity>
        where TEntity : EntityTree<TKey, TEntity>, new()
    {
        #region 构造
        //static EntityTree()
        //{
        //    Meta.OnDataChange += delegate { Root = null; };
        //}
        #endregion

        #region 扩展属性
        /// <summary>关联键名称</summary>
        protected virtual String KeyName
        {
            get { return Meta.Unique.Name; }
        }

        /// <summary>关联父键名</summary>
        protected virtual String ParentKeyName
        {
            get { return "Parent" + KeyName; }
        }

        /// <summary>排序字段</summary>
        protected virtual String SortingKeyName
        {
            get
            {
                String[] names = new String[] { "Sorting", "Rank" };
                foreach (String name in names)
                {
                    if (Meta.FieldNames.Contains(name)) return name;
                }
                return null;
            }
        }

        /// <summary>子节点</summary>
        public virtual EntityList<TEntity> Childs
        {
            get { return GetExtend<EntityList<TEntity>>("Childs", delegate { return FindChilds(); }); }
            set { SetExtend("Childs", value); }
        }

        /// <summary>子节点</summary>
        protected virtual EntityList<TEntity> FindChilds()
        {
            return FindAllByParent(this[KeyName]);
        }

        /// <summary>父节点</summary>
        [XmlIgnore]
        public virtual TEntity Parent
        {
            get { return GetExtend<TEntity>("Parent", delegate { return FindParent(); }); }
            set { SetExtend("Parent", value); }
        }

        /// <summary>父节点</summary>
        protected virtual TEntity FindParent()
        {
            return Meta.Cache.Entities.Find(KeyName, this[ParentKeyName]);
        }

        /// <summary>子孙节点</summary>
        [XmlIgnore]
        public virtual EntityList<TEntity> AllChilds
        {
            get { return GetExtend<EntityList<TEntity>>("AllChilds", delegate { return FindAllChilds(this); }); }
            set { SetExtend("AllChilds", value); }
        }

        /// <summary>父节点集合</summary>
        [XmlIgnore]
        public virtual EntityList<TEntity> AllParents
        {
            get { return GetExtend<EntityList<TEntity>>("AllParents", delegate { return FindAllParents(this); }); }
            set { SetExtend("AllParents", value); }
        }

        /// <summary>深度</summary>
        [XmlIgnore]
        public virtual Int32 Deepth
        {
            get
            {
                Int32 _Deepth = 1;
                if (AllParents != null && AllParents.Count > 0) _Deepth += AllParents.Count;
                return _Deepth;
            }
        }

        private static TEntity _Root;
        /// <summary>根</summary>
        public static TEntity Root
        {
            get
            {
                if (_Root == null)
                {
                    _Root = new TEntity();
                    Meta.OnDataChange += delegate { _Root = null; };
                }
                return _Root;
            }
            set { _Root = null; }
        }
        #endregion

        #region 查询
        /// <summary>
        /// 根据父级查找所有子级，带排序功能
        /// </summary>
        /// <param name="parentKey"></param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select)]
        public static EntityList<TEntity> FindAllByParent(Object parentKey)
        {
            TEntity entity = EntityFactory.CreateOperate(typeof(TEntity)) as TEntity;

            EntityList<TEntity> list = Meta.Cache.Entities.FindAll(entity.ParentKeyName, parentKey);
            if (list == null || list.Count < 1) return null;

            String sort = entity.SortingKeyName;
            if (!String.IsNullOrEmpty(sort))
            {
                list.Sort(delegate(TEntity item1, TEntity item2)
                {
                    if (item1[sort] != item2[sort])
                        return -1 * (item1[sort] as IComparable).CompareTo(item2[sort]);
                    else
                        return (item1[entity.KeyName] as IComparable).CompareTo(item2[entity.KeyName]);
                });
            }
            return list;
        }
        #endregion

        #region 树形计算
        /// <summary>
        /// 查找指定节点的所有子节点，以深度层次树结构输出
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected static EntityList<TEntity> FindAllChilds(IEntityTree<TEntity> entity)
        {
            //XTrace.WriteLine("FindAllChilds ", entity);
            if (entity == null || entity.Childs == null || entity.Childs.Count < 1) return null;

            EntityList<TEntity> list = new EntityList<TEntity>();
            // 使用队列而不使用递归，避免死循环
            // 使用队列而不使用堆栈，因为树的构造一般是深度搜索而不是广度搜索
            Stack<TEntity> stack = new Stack<TEntity>();
            stack.Push(entity as TEntity);

            while (stack.Count > 0)
            {
                TEntity item = stack.Pop();
                if (list.Contains(item)) continue;
                list.Add(item);

                EntityList<TEntity> childs = item.Childs;
                if (childs == null || childs.Count < 1) continue;

                // 反向入队
                for (int i = childs.Count - 1; i >= 0; i--)
                {
                    // 已计算到结果的，不再处理
                    if (list.Contains(childs[i])) continue;
                    // 已进入待处理队列的，不再处理
                    if (stack.Contains(childs[i])) continue;

                    stack.Push(childs[i]);
                }
            }
            // 去掉第一个，那是自身
            list.RemoveAt(0);
            //XTrace.WriteLine("FindAllChilds Count={0}", list.Count);

            return list.Count > 0 ? list : null;
        }

        /// <summary>
        /// 查找指定节点的所有父节点，从高到底以深度层次树结构输出
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        protected static EntityList<TEntity> FindAllParents(IEntityTree<TEntity> entity)
        {
            if (entity == null || entity.Parent == null) return null;

            EntityList<TEntity> list = new EntityList<TEntity>();
            TEntity item = entity as TEntity;
            while (item != null)
            {
                // 形成了死循环，就此中断
                if (list.Contains(item)) break;

                list.Add(item);

                item = item.Parent;
            }
            // 去掉第一个自己
            list.RemoveAt(0);
            if (list == null || list.Count < 1) return null;

            // 反转
            list.Reverse();

            return list;
        }
        #endregion

        #region 业务
        /// <summary>
        /// 创建菜单树
        /// </summary>
        /// <param name="nodes">父集合</param>
        /// <param name="list">菜单列表</param>
        /// <param name="url">格式化地址，可以使用{ID}和{Name}</param>
        /// <param name="func">由菜单项创建树节点的委托</param>
        public static void MakeTree(TreeNodeCollection nodes, EntityList<TEntity> list, String url, Func<TEntity, TreeNode> func)
        {
            if (list == null || list.Count < 1) return;

            // 使用内层递归，避免死循环
            MakeTree(nodes, list, url, func, new EntityList<TEntity>());
        }

        private static void MakeTree(TreeNodeCollection nodes, EntityList<TEntity> list, String url, Func<TEntity, TreeNode> func, EntityList<TEntity> parents)
        {
            String id = Meta.FieldNames[0];
            String name = Meta.FieldNames[1];
            if (Meta.FieldNames.Contains("Name")) name = "Name";
            if (Meta.Unique != null)
                id = Meta.Unique.Name;
            else if (Meta.FieldNames.Contains("ID"))
                id = "ID";

            foreach (TEntity item in list)
            {
                if (parents.Contains(item) || parents.Exists(id, item[id])) continue;
                parents.Add(item);

                TreeNode node = null;
                if (func == null)
                {
                    node = new TreeNode((String)item[name]);
                    node.Value = "" + item[id];
                    if (!String.IsNullOrEmpty(url))
                    {
                        foreach (String elm in Meta.FieldNames)
                        {
                            url = url.Replace("{" + elm + "}", "" + item[elm]);
                        }
                        node.NavigateUrl = url;
                    }
                }
                else
                {
                    node = func(item);
                }

                if (item.Childs != null && item.Childs.Count > 0) MakeTree(node.ChildNodes, item.Childs, url, func, parents);

                if (node != null) nodes.Add(node);
            }
        }

        /// <summary>
        /// 取得全路径的实体，由上向下排序
        /// </summary>
        /// <param name="includeSelf"></param>
        /// <returns></returns>
        public EntityList<TEntity> GetFullPath(Boolean includeSelf)
        {
            EntityList<TEntity> list = AllParents;

            if (!includeSelf) return list;

            if (list == null) list = new EntityList<TEntity>();
            list.Add(this as TEntity);

            return list;
        }

        /// <summary>
        /// 删除子级到本级的关系。导出数据前可以先删除关系，以减少导出的大小
        /// </summary>
        public virtual void ClearRelation()
        {
            if (Childs == null || Childs.Count < 1) return;

            foreach (TEntity item in Childs)
            {
                item[KeyName] = default(TKey);
                item[ParentKeyName] = default(TKey);

                item.ClearRelation();
            }
        }

        /// <summary>
        /// 批量保存，保存整棵树
        /// </summary>
        /// <param name="saveSelf">是否保存自己</param>
        /// <returns></returns>
        public virtual Int32 BatchSave(Boolean saveSelf)
        {
            Int32 count = 0;

            Meta.BeginTrans();
            try
            {
                EntityList<TEntity> list = Childs;
                if (saveSelf) count += Save();
                // 上面保存数据后，可能会引起扩展属性抖动（不断更新）
                if (list != null && list.Count > 0)
                {
                    foreach (TEntity item in list)
                    {
                        item[ParentKeyName] = this[KeyName];
                        count += item.BatchSave(true);
                    }
                }

                Meta.Commit();

                return count;
            }
            catch
            {
                Meta.Rollback();
                throw;
            }
        }
        #endregion

        #region 数据检查
        /// <summary>
        /// 验证树形数据是否有效
        /// </summary>
        public virtual void Valid()
        {
            TKey key = (TKey)this[KeyName];
            TKey pkey = (TKey)this[ParentKeyName];

            Boolean isnull = IsNull(key);
            Boolean pisnull = IsNull(pkey);

            // 无主检查
            //if (!Meta.Cache.Entities.Exists(KeyName, pkey)) throw new Exception("无效上级[" + pkey + "]！");
            if (!pisnull && FindCount(KeyName, pkey) <= 0) throw new Exception("无效上级[" + pkey + "]！");

            // 死循环检查
            if (isnull)
            {
                // 插入状态，key为空，pkey可以是任何值
            }
            else
            {
                // 更新状态，且pkey不为空时，判断两者是否相等
                if (!pisnull && Object.Equals(pkey, key)) throw new Exception("上级不能是当前节点！");
            }

            // 编辑状态且设置了父节点时才处理
            if (!isnull && !pisnull)
            {
                EntityList<TEntity> list = this.AllChilds;
                if (list != null && list.Exists(KeyName, pkey))
                    throw new Exception("上级[" + pkey + "]是当前节点的子孙节点！");
            }
        }

        private static Boolean IsNull(TKey value)
        {
            // 为空或者默认值，返回空
            if (value == null || Object.Equals(value, default(TKey))) return true;

            // 字符串的空
            if (typeof(TKey) == typeof(String) && String.IsNullOrEmpty(value.ToString())) return true;

            return false;
        }

        /// <summary>
        /// 已重载。操作前验证树形数据是否有效
        /// </summary>
        /// <returns></returns>
        public override int Insert()
        {
            Valid();
            return base.Insert();
        }

        /// <summary>
        /// 已重载。操作前验证树形数据是否有效
        /// </summary>
        /// <returns></returns>
        public override int Update()
        {
            Valid();
            return base.Update();
        }
        #endregion
    }
}