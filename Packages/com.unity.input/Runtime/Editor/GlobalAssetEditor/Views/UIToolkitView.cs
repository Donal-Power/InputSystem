using System;
using System.Collections.Generic;

namespace UnityEngine.InputSystem.Editor
{
    internal interface IViewStateSelector<out TViewState>
    {
        bool HasStateChanged(GlobalInputActionsEditorState state);
        TViewState GetViewState(GlobalInputActionsEditorState state);
    }

    internal interface IUIToolkitView
    {
        void UpdateView(GlobalInputActionsEditorState state);
        void DestroyView();
    }

    internal abstract class UIToolkitView<TViewState> : IUIToolkitView
    {
        protected readonly StateContainer stateContainer;
        private IViewStateSelector<TViewState> m_ViewStateSelector;
        private IList<IUIToolkitView> m_ChildViews;
        private bool m_IsFirstUpdate = true;

        protected UIToolkitView(StateContainer stateContainer)
        {
            this.stateContainer = stateContainer;
            m_ChildViews = new List<IUIToolkitView>();
        }

        protected void OnStateChanged(GlobalInputActionsEditorState state)
        {
            UpdateView(state);
        }

        public void UpdateView(GlobalInputActionsEditorState state)
        {
            if (m_ViewStateSelector == null)
            {
                Debug.LogWarning($"View '{GetType().Name}' has no selector and will not render. Create a selector for the" +
                    $"view using the CreateSelector method.");
                return;
            }

            if (m_ViewStateSelector.HasStateChanged(state) || m_IsFirstUpdate)
                RedrawUI(m_ViewStateSelector.GetViewState(state));

            m_IsFirstUpdate = false;
            foreach (var view in m_ChildViews)
            {
                view.UpdateView(state);
            }
        }

        public TView CreateChildView<TView>(TView view) where TView : IUIToolkitView
        {
            m_ChildViews.Add(view);
            return view;
        }

        public void DestroyView<TView>(TView view) where TView : IUIToolkitView
        {
            if (view == null)
                return;

            m_ChildViews.Remove(view);
            view.DestroyView();
        }

        public void Dispatch(Command command)
        {
            stateContainer.Dispatch(command);
        }

        public virtual void RedrawUI(TViewState viewState)
        {
        }

        /// <summary>
        /// Called when a parent view is destroying this view to give it an opportunity to clean up any
        /// resources or event handlers.
        /// </summary>
        public virtual void DestroyView()
        {
        }

        protected void CreateSelector(Func<GlobalInputActionsEditorState, TViewState> selector)
        {
            m_ViewStateSelector = new ViewStateSelector<TViewState>(selector);
        }

        protected void CreateSelector<T1>(
            Func<GlobalInputActionsEditorState, T1> func1,
            Func<T1, GlobalInputActionsEditorState, TViewState> selector)
        {
            m_ViewStateSelector = new ViewStateSelector<T1, TViewState>(func1, selector);
        }

        protected void CreateSelector<T1, T2>(
            Func<GlobalInputActionsEditorState, T1> func1,
            Func<GlobalInputActionsEditorState, T2> func2,
            Func<T1, T2, GlobalInputActionsEditorState, TViewState> selector)
        {
            m_ViewStateSelector = new ViewStateSelector<T1, T2, TViewState>(func1, func2, selector);
        }

        protected void CreateSelector<T1, T2, T3>(
            Func<GlobalInputActionsEditorState, T1> func1,
            Func<GlobalInputActionsEditorState, T2> func2,
            Func<GlobalInputActionsEditorState, T3> func3,
            Func<T1, T2, T3, GlobalInputActionsEditorState, TViewState> selector)
        {
            m_ViewStateSelector = new ViewStateSelector<T1, T2, T3, TViewState>(func1, func2, func3, selector);
        }
    }

    internal class ViewStateSelector<TReturn> : IViewStateSelector<TReturn>
    {
        private readonly Func<GlobalInputActionsEditorState, TReturn> m_Selector;

        public ViewStateSelector(Func<GlobalInputActionsEditorState, TReturn> selector)
        {
            m_Selector = selector;
        }

        public bool HasStateChanged(GlobalInputActionsEditorState state)
        {
            return true;
        }

        public TReturn GetViewState(GlobalInputActionsEditorState state)
        {
            return m_Selector(state);
        }
    }

    // TODO: Make all args to view state selectors IEquatable<T>?
    internal class ViewStateSelector<T1, TReturn> : IViewStateSelector<TReturn>
    {
        private readonly Func<GlobalInputActionsEditorState, T1> m_Func1;
        private readonly Func<T1, GlobalInputActionsEditorState, TReturn> m_Selector;

        private T1 m_PreviousT1;

        public ViewStateSelector(Func<GlobalInputActionsEditorState, T1> func1,
                                 Func<T1, GlobalInputActionsEditorState, TReturn> selector)
        {
            m_Func1 = func1;
            m_Selector = selector;
        }

        public bool HasStateChanged(GlobalInputActionsEditorState state)
        {
            var valueOne = m_Func1(state);

            if (valueOne is IViewStateCollection collection)
            {
                if (collection.SequenceEqual((IViewStateCollection)m_PreviousT1))
                    return false;
            }
            else if (valueOne.Equals(m_PreviousT1))
            {
                return false;
            }

            m_PreviousT1 = valueOne;
            return true;
        }

        public TReturn GetViewState(GlobalInputActionsEditorState state)
        {
            return m_Selector(m_PreviousT1, state);
        }
    }

    internal class ViewStateSelector<T1, T2, TReturn> : IViewStateSelector<TReturn>
    {
        private readonly Func<GlobalInputActionsEditorState, T1> m_Func1;
        private readonly Func<GlobalInputActionsEditorState, T2> m_Func2;
        private readonly Func<T1, T2, GlobalInputActionsEditorState, TReturn> m_Selector;

        private T1 m_PreviousT1;
        private T2 m_PreviousT2;

        public ViewStateSelector(Func<GlobalInputActionsEditorState, T1> func1,
                                 Func<GlobalInputActionsEditorState, T2> func2,
                                 Func<T1, T2, GlobalInputActionsEditorState, TReturn> selector)
        {
            m_Func1 = func1;
            m_Func2 = func2;
            m_Selector = selector;
        }

        public bool HasStateChanged(GlobalInputActionsEditorState state)
        {
            var valueOne = m_Func1(state);
            var valueTwo = m_Func2(state);

            var valueOneHasChanged = false;
            var valueTwoHasChanged = false;

            if (valueOne is IViewStateCollection collection && !collection.SequenceEqual((IViewStateCollection)m_PreviousT1) ||
                !valueOne.Equals(m_PreviousT1))
                valueOneHasChanged = true;

            if (valueTwo is IViewStateCollection collection2 && !collection2.SequenceEqual((IViewStateCollection)m_PreviousT2) ||
                !valueTwo.Equals(m_PreviousT2))
                valueTwoHasChanged = true;

            if (!valueOneHasChanged && !valueTwoHasChanged)
                return false;

            m_PreviousT1 = valueOne;
            m_PreviousT2 = valueTwo;
            return true;
        }

        public TReturn GetViewState(GlobalInputActionsEditorState state)
        {
            return m_Selector(m_PreviousT1, m_PreviousT2, state);
        }
    }

    internal class ViewStateSelector<T1, T2, T3, TReturn> : IViewStateSelector<TReturn>
    {
        private readonly Func<GlobalInputActionsEditorState, T1> m_Func1;
        private readonly Func<GlobalInputActionsEditorState, T2> m_Func2;
        private readonly Func<GlobalInputActionsEditorState, T3> m_Func3;
        private readonly Func<T1, T2, T3, GlobalInputActionsEditorState, TReturn> m_Selector;

        private T1 m_PreviousT1;
        private T2 m_PreviousT2;
        private T3 m_PreviousT3;

        public ViewStateSelector(Func<GlobalInputActionsEditorState, T1> func1,
                                 Func<GlobalInputActionsEditorState, T2> func2,
                                 Func<GlobalInputActionsEditorState, T3> func3,
                                 Func<T1, T2, T3, GlobalInputActionsEditorState, TReturn> selector)
        {
            m_Func1 = func1;
            m_Func2 = func2;
            m_Func3 = func3;
            m_Selector = selector;
        }

        public bool HasStateChanged(GlobalInputActionsEditorState state)
        {
            var valueOne = m_Func1(state);
            var valueTwo = m_Func2(state);
            var valueThree = m_Func3(state);

            var valueOneHasChanged = false;
            var valueTwoHasChanged = false;
            var valueThreeHasChanged = false;

            if (valueOne is IViewStateCollection collection && !collection.SequenceEqual((IViewStateCollection)m_PreviousT1) ||
                !valueOne.Equals(m_PreviousT1))
                valueOneHasChanged = true;

            if (valueTwo is IViewStateCollection collection2 && !collection2.SequenceEqual((IViewStateCollection)m_PreviousT2) ||
                !valueTwo.Equals(m_PreviousT2))
                valueTwoHasChanged = true;

            if (valueThree is IViewStateCollection collection3 && !collection3.SequenceEqual((IViewStateCollection)m_PreviousT3) ||
                !valueThree.Equals(m_PreviousT3))
                valueThreeHasChanged = true;

            if (!valueOneHasChanged && !valueTwoHasChanged && !valueThreeHasChanged)
                return false;

            m_PreviousT1 = valueOne;
            m_PreviousT2 = valueTwo;
            m_PreviousT3 = valueThree;
            return true;
        }

        public TReturn GetViewState(GlobalInputActionsEditorState state)
        {
            return m_Selector(m_PreviousT1, m_PreviousT2, m_PreviousT3, state);
        }
    }
}
