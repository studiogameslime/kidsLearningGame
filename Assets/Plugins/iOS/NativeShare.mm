#import <UIKit/UIKit.h>

extern "C" {
    void _ShareText(const char* text) {
        NSString *shareText = [NSString stringWithUTF8String:text];
        NSArray *items = @[shareText];

        UIActivityViewController *activityVC =
            [[UIActivityViewController alloc] initWithActivityItems:items
                                             applicationActivities:nil];

        UIViewController *rootVC =
            [UIApplication sharedApplication].keyWindow.rootViewController;

        // iPad requires popover
        if (UI_USER_INTERFACE_IDIOM() == UIUserInterfaceIdiomPad) {
            activityVC.popoverPresentationController.sourceView = rootVC.view;
            activityVC.popoverPresentationController.sourceRect =
                CGRectMake(rootVC.view.bounds.size.width / 2, rootVC.view.bounds.size.height / 2, 0, 0);
        }

        [rootVC presentViewController:activityVC animated:YES completion:nil];
    }
}
