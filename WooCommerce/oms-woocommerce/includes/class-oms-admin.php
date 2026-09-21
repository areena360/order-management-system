<?php
defined('ABSPATH') || exit;

final class OMS_Woo_Admin {
    public static function init(): void {
        add_action('admin_menu', static function () {
            add_submenu_page('woocommerce', 'OMS Integration', 'OMS Integration', 'manage_woocommerce', 'oms-woocommerce', array(__CLASS__, 'page'));
        });
        add_action('admin_post_oms_woo_action', array(__CLASS__, 'handle'));
        add_action('woocommerce_admin_order_data_after_shipping_address', static function ($order) {
            $tracking = $order->get_meta('_oms_tracking_number');
            if ($tracking) { echo '<p><strong>OMS tracking:</strong> ' . esc_html($tracking) . '</p>'; }
        });
    }
    private static function require_admin(): void {
        if (!current_user_can('manage_woocommerce')) { wp_die('You do not have permission to manage this integration.'); }
    }
    private static function form(string $action, string $label, string $fields = ''): void {
        echo '<form method="post" action="' . esc_url(admin_url('admin-post.php')) . '" style="margin:16px 0">';
        wp_nonce_field('oms_woo_' . $action);
        echo '<input type="hidden" name="action" value="oms_woo_action"><input type="hidden" name="operation" value="' . esc_attr($action) . '">';
        echo $fields; // Only static markup and escaped field values from this class.
        submit_button($label, 'secondary', 'submit', false);
        echo '</form>';
    }
    public static function handle(): void {
        self::require_admin();
        $action = sanitize_key(wp_unslash($_POST['operation'] ?? ''));
        check_admin_referer('oms_woo_' . $action);
        $c = OMS_Woo_API::config();
        $message = '';
        if ($action === 'connect') {
            $api = untrailingslashit(esc_url_raw(wp_unslash($_POST['api_url'] ?? '')));
            $web = untrailingslashit(esc_url_raw(wp_unslash($_POST['web_url'] ?? '')));
            foreach (array($api, $web, home_url()) as $url) {
                $parts = wp_parse_url($url);
                if (!$parts || (($parts['scheme'] ?? '') !== 'https' && !OMS_Woo_API::local_url($url)) || empty($parts['host']) || isset($parts['user']) || isset($parts['pass']) || isset($parts['query']) || isset($parts['fragment'])) {
                    wp_die('Use HTTPS addresses, or explicitly enable localhost development. Credentials, query strings and fragments are not allowed.');
                }
            }
            if (OMS_Woo_API::connected() && ($api !== $c['api_url'] || $web !== $c['web_url'])) {
                wp_die('Disconnect the current OMS before changing its address.');
            }
            $c['api_url'] = $api; $c['web_url'] = $web;
            $c['pending_secret'] = bin2hex(random_bytes(32));
            OMS_Woo_API::save($c);
            $result = OMS_Woo_API::request('authorize/start', array(
                'installationId' => get_option('oms_woo_installation'), 'storeName' => get_bloginfo('name'),
                'storeUrl' => untrailingslashit(home_url()), 'tokenHash' => hash('sha256', $c['pending_secret']), 'pluginVersion' => OMS_WOO_VERSION));
            if (is_wp_error($result)) { $message = $result->get_error_message(); }
            else {
                $c['device_id'] = $result['deviceId']; $c['approval_code'] = $result['code'];
                $c['expires_at'] = $result['expiresAt']; OMS_Woo_API::save($c);
                $message = 'Open OMS authorization below, approve the store, then click Finish connection.';
            }
        } elseif ($action === 'finish') {
            if (empty($c['device_id'])) { wp_die('Start authorization first.'); }
            $result = OMS_Woo_API::request('authorize/poll/' . rawurlencode($c['device_id']), array(), true);
            if (is_wp_error($result)) { $message = $result->get_error_message() . ' If expired, start authorization again.'; }
            elseif (($result['state'] ?? '') === 'approved') {
                $c['secret'] = $c['pending_secret']; $c['connection_id'] = (int) $result['connectionId'];
                unset($c['pending_secret'], $c['device_id'], $c['approval_code'], $c['expires_at']);
                OMS_Woo_API::save($c);
                update_option('oms_woo_updates_cursor', 0, false);
                if (!get_option('oms_woo_reconcile_from')) { update_option('oms_woo_reconcile_from', time() - 120, false); }
                $message = 'Connected. New orders sync automatically. Choose an import range below.';
            } else { $message = 'Waiting for approval in OMS.'; }
        } elseif ($action === 'disconnect') {
            if (empty($_POST['confirm_disconnect'])) { wp_die('Confirm disconnection first.'); }
            $result = OMS_Woo_API::request('disconnect', array());
            if (is_wp_error($result) && (int) ($result->get_error_data()['status'] ?? 0) !== 401) {
                $message = 'Could not confirm server revocation. Disconnect in OMS, then reconnect or retry. ' . $result->get_error_message();
            } else {
                unset($c['secret'], $c['connection_id'], $c['pending_secret'], $c['device_id'], $c['approval_code'], $c['expires_at']);
                OMS_Woo_API::save($c);
                delete_option('oms_woo_scan_state');
                foreach (array('oms_woo_send', 'oms_woo_scan') as $hook) { as_unschedule_all_actions($hook, null, 'oms-woo'); }
                $message = 'Disconnected. Existing OMS orders retained.';
            }
        } elseif ($action === 'reset_local') {
            if (empty($_POST['confirm_reset_local'])) { wp_die('Confirm local credential removal first.'); }
            unset($c['secret'], $c['connection_id'], $c['pending_secret'], $c['device_id'], $c['approval_code'], $c['expires_at']);
            OMS_Woo_API::save($c);
            delete_option('oms_woo_scan_state');
            delete_option('oms_woo_sync_request');
            update_option('oms_woo_updates_cursor', 0, false);
            update_option('oms_woo_reconcile_from', time(), false);
            foreach (array('oms_woo_send', 'oms_woo_scan') as $hook) { as_unschedule_all_actions($hook, null, 'oms-woo'); }
            $message = 'Local credential removed. Orders retained. Enter the new URLs and reconnect to the same OMS account to rotate the old credential. This action did not revoke the credential on the server.';
        } elseif ($action === 'import') {
            if (!OMS_Woo_API::connected()) { wp_die('Connect first.'); }
            $range = sanitize_key(wp_unslash($_POST['range'] ?? '30'));
            $from = 0;
            if (in_array($range, array('30', '90'), true)) { $from = time() - (int) $range * DAY_IN_SECONDS; }
            elseif ($range === 'custom') {
                $date = sanitize_text_field(wp_unslash($_POST['date_from'] ?? ''));
                $parsed = DateTimeImmutable::createFromFormat('!Y-m-d', $date, new DateTimeZone('UTC'));
                if (!$parsed || $parsed->format('Y-m-d') !== $date || $parsed->getTimestamp() > time()) { wp_die('Enter a valid past start date.'); }
                $from = $parsed->getTimestamp();
            } elseif ($range !== 'all') { wp_die('Invalid import range.'); }
            $message = OMS_Woo_Sync::start_scan($from) ? 'Import queued in background.' : 'An import is already running.';
        } elseif ($action === 'retry') {
            OMS_Woo_Sync::queue(absint($_POST['order_id'] ?? 0));
            $message = 'Order queued for retry.';
        } elseif ($action === 'sync') {
            as_schedule_single_action(time() + 1, 'oms_woo_tick', array(), 'oms-woo');
            $message = 'Synchronization queued.';
        }
        set_transient('oms_woo_notice_' . get_current_user_id(), $message, 60);
        wp_safe_redirect(admin_url('admin.php?page=oms-woocommerce'));
        exit;
    }
    public static function page(): void {
        self::require_admin();
        $c = OMS_Woo_API::config();
        echo '<div class="wrap"><h1>OMS WooCommerce Connector</h1>';
        $notice = get_transient('oms_woo_notice_' . get_current_user_id());
        if ($notice) { echo '<div class="notice notice-info"><p>' . esc_html($notice) . '</p></div>'; delete_transient('oms_woo_notice_' . get_current_user_id()); }
        echo '<p><strong>Connection:</strong> ' . (OMS_Woo_API::connected() ? 'Credential configured' : 'Disconnected') . '</p>';
        echo '<p>Last successful order sync: ' . esc_html(get_option('oms_woo_last_sync', 'Never')) . '</p>';
        echo '<p>Import: ' . esc_html(get_option('oms_woo_scan_state') ? 'Running in background' : get_option('oms_woo_last_import', 'Not started')) . '</p>';
        echo '<h2>Connect / rotate credential</h2><p>Use the HTTPS addresses supplied by your OMS administrator. No WordPress password is sent to OMS.</p>';
        self::form('connect', 'Connect to OMS / reauthorize',
            '<p><label>OMS API URL (ending /api)<br><input class="regular-text" type="url" name="api_url" required value="' . esc_attr($c['api_url'] ?? '') . '" placeholder="https://api.example.com/api"></label></p>' .
            '<p><label>OMS web URL<br><input class="regular-text" type="url" name="web_url" required value="' . esc_attr($c['web_url'] ?? '') . '" placeholder="https://oms.example.com"></label></p>');
        if (!empty($c['approval_code'])) {
            $link = $c['web_url'] . '/dashboard/integrations/woocommerce?code=' . rawurlencode($c['approval_code']);
            echo '<p><a class="button button-primary" target="_blank" rel="noopener noreferrer" href="' . esc_url($link) . '">Open OMS authorization</a></p>';
            echo '<p>Authorization expires: ' . esc_html($c['expires_at']) . '. Verify the store address in OMS before approving.</p>';
            self::form('finish', 'Finish connection');
        }
        if (OMS_Woo_API::connected()) {
            echo '<h2>Import existing orders</h2>';
            self::form('import', 'Start import', '<p><label>Range <select name="range"><option value="30">Last 30 days</option><option value="90">Last 90 days</option><option value="all">All orders</option><option value="custom">Custom start date</option></select></label> <label>Start date (UTC) <input type="date" name="date_from"></label></p>');
            self::form('sync', 'Sync now');
            self::form('retry', 'Retry order', '<label>WooCommerce order ID <input type="number" min="1" name="order_id" required></label> ');
            echo '<h2>Disconnect</h2><p>Existing imported OMS orders will not be deleted.</p>';
            self::form('disconnect', 'Disconnect', '<p><label><input type="checkbox" name="confirm_disconnect" value="1" required> Stop future sync and revoke this credential.</label></p>');
            echo '<details><summary>Old server or tunnel unavailable?</summary><p>Remove the local credential to enter a replacement URL. Orders are retained. This does not revoke the server credential: disconnect in OMS first, or reconnect to the same OMS account to rotate it.</p>';
            self::form('reset_local', 'Reset local connection', '<p><label><input type="checkbox" name="confirm_reset_local" value="1" required> I understand this clears only the WordPress credential and pauses sync until I reconnect.</label></p>');
            echo '</details>';
        }
        echo '<h2>Recent sync logs</h2><p>Background processing uses WooCommerce → Status → Scheduled Actions. Configure a real WordPress cron on low-traffic stores.</p><table class="widefat striped"><thead><tr><th>UTC time</th><th>Order</th><th>Result</th><th>Message</th></tr></thead><tbody>';
        foreach ((array) get_option('oms_woo_logs', array()) as $log) {
            echo '<tr><td>' . esc_html($log['at']) . '</td><td>' . esc_html((string) $log['order']) . '</td><td>' . esc_html($log['result']) . '</td><td>' . esc_html($log['message']) . '</td></tr>';
        }
        echo '</tbody></table></div>';
    }
}
