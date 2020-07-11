namespace Fabulous.XamarinForms

[<AutoOpen>]
module BarCodeViewExtension =

    open Fabulous
    open ZXing.Net.Mobile.Forms

    let OnScanResultAttribKey = AttributeKey<ZXingScannerView.ScanResultDelegate> "OnScanResult"

    type ViewElement with 
        /// Update an event handler on a target control, given a previous and current view element description
        member inline source.UpdateZxingEvent(prevOpt: ViewElement voption, attribKey: AttributeKey<ZXingScannerView.ScanResultDelegate>, targetAdd: ZXingScannerView.ScanResultDelegate -> unit, targetRemove: ZXingScannerView.ScanResultDelegate -> unit ) = 
                let prevValueOpt = match prevOpt with ValueNone -> ValueNone | ValueSome prev -> prev.TryGetAttributeKeyed<ZXingScannerView.ScanResultDelegate>(attribKey)
                let valueOpt = source.TryGetAttributeKeyed<ZXingScannerView.ScanResultDelegate>(attribKey)
                match prevValueOpt, valueOpt with
                | ValueSome prevValue, ValueSome currValue when identical prevValue currValue -> ()
                | ValueSome prevValue, ValueSome currValue -> targetAdd(prevValue); targetRemove(currValue)
                | ValueNone, ValueSome currValue -> targetAdd(currValue)
                | ValueSome prevValue, ValueNone -> targetRemove(prevValue)
                | ValueNone, ValueNone -> ()

    type View with
        static member BarCodeScanner (?onScanResult : ( ZXing.Result -> unit),
                                          // inherited attributes common to all views
                                          ?gestureRecognizers, ?horizontalOptions, ?margin, ?verticalOptions, ?anchorX, ?anchorY, ?backgroundColor,
                                          ?behaviors, ?flowDirection, ?height, ?inputTransparent, ?isEnabled, ?isTabStop, ?isVisible, ?minimumHeight,
                                          ?minimumWidth, ?opacity, ?resources, ?rotation, ?rotationX, ?rotationY, ?scale, ?scaleX, ?scaleY, ?styles,
                                          ?styleSheets, ?tabIndex, ?translationX, ?translationY, ?visual, ?width, ?style, ?styleClasses, ?shellBackButtonBehavior,
                                          ?shellBackgroundColor, ?shellDisabledColor, ?shellForegroundColor, ?shellFlyoutBehavior, ?shellNavBarIsVisible,
                                          ?shellSearchHandler, ?shellTabBarBackgroundColor, ?shellTabBarDisabledColor, ?shellTabBarForegroundColor,
                                          ?shellTabBarIsVisible, ?shellTabBarTitleColor, ?shellTabBarUnselectedColor, ?shellTitleColor, ?shellTitleView,
                                          ?shellUnselectedColor, ?automationId, ?classId, ?effects, ?menu, ?ref, ?styleId, ?tag, ?focused, ?unfocused, ?created) =

            // Count the number of additional attributes
            let attribCount = 0
            let attribCount = match onScanResult with Some _ -> attribCount + 1 | None -> attribCount

            // Unbox the ViewRef
            let viewRef = match ref with None -> None | Some (ref: ViewRef<ZXing.Net.Mobile.Forms.ZXingScannerView>) -> Some ref.Unbox

            // Populate the attributes of the base element
            let attribs = 
                ViewBuilders.BuildView(attribCount, ?gestureRecognizers=gestureRecognizers, ?horizontalOptions=horizontalOptions, ?margin=margin,
                                       ?verticalOptions=verticalOptions, ?anchorX=anchorX, ?anchorY=anchorY, ?backgroundColor=backgroundColor, ?behaviors=behaviors,
                                       ?flowDirection=flowDirection, ?height=height, ?inputTransparent=inputTransparent, ?isEnabled=isEnabled, ?isTabStop=isTabStop,
                                       ?isVisible=isVisible, ?minimumHeight=minimumHeight, ?minimumWidth=minimumWidth, ?opacity=opacity, ?resources=resources,
                                       ?rotation=rotation, ?rotationX=rotationX, ?rotationY=rotationY, ?scale=scale, ?scaleX=scaleX, ?scaleY=scaleY, ?styles=styles,
                                       ?styleSheets=styleSheets, ?tabIndex=tabIndex, ?translationX=translationX, ?translationY=translationY, ?visual=visual, ?width=width,
                                       ?style=style, ?styleClasses=styleClasses, ?shellBackButtonBehavior=shellBackButtonBehavior, ?shellBackgroundColor=shellBackgroundColor,
                                       ?shellDisabledColor=shellDisabledColor, ?shellForegroundColor=shellForegroundColor, ?shellFlyoutBehavior=shellFlyoutBehavior,
                                       ?shellNavBarIsVisible=shellNavBarIsVisible, ?shellSearchHandler=shellSearchHandler, ?shellTabBarBackgroundColor=shellTabBarBackgroundColor,
                                       ?shellTabBarDisabledColor=shellTabBarDisabledColor, ?shellTabBarForegroundColor=shellTabBarForegroundColor,
                                       ?shellTabBarIsVisible=shellTabBarIsVisible, ?shellTabBarTitleColor=shellTabBarTitleColor, ?shellTabBarUnselectedColor=shellTabBarUnselectedColor,
                                       ?shellTitleColor=shellTitleColor, ?shellTitleView=shellTitleView, ?shellUnselectedColor=shellUnselectedColor, ?automationId=automationId,
                                       ?classId=classId, ?effects=effects, ?menu=menu, ?ref=viewRef, ?styleId=styleId, ?tag=tag, ?focused=focused, ?unfocused=unfocused, ?created=created)
            
            match onScanResult with None -> () | Some v -> attribs.Add(OnScanResultAttribKey,ZXingScannerView.ScanResultDelegate (fun result -> v (result) ))

            //let ScanResultDelegate = new ZXingScannerView.ScanResultDelegate(onScanResult)
       
            // The create method
            let create () =
                Xamarin.Essentials.Permissions.RequestAsync<Xamarin.Essentials.Permissions.Flashlight>().Wait()
                let instance =
                  new ZXingScannerView (
                    IsAnalyzing = true,
                    IsScanning = true,
                    WidthRequest = 300.,
                    HeightRequest = 300.,
                    IsTorchOn = true,
                    Options = ZXing.Mobile.MobileBarcodeScanningOptions(
                      PossibleFormats = ResizeArray(seq { ZXing.BarcodeFormat.QR_CODE })
                    )
                  )
                //instance.add_OnScanResult(ScanResultDelegate)
                instance

            // The update method
            let update (prevOpt : ViewElement voption) (source: ViewElement) (target : ZXingScannerView) = 
                ViewBuilders.UpdateView (prevOpt, source, target)
                source.UpdateZxingEvent(prevOpt, OnScanResultAttribKey, target.add_OnScanResult, target.remove_OnScanResult)


            // The element
            ViewElement.Create<ZXingScannerView>(create, update, attribs)

