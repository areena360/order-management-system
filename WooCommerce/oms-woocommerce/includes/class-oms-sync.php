<?php
defined('ABSPATH') || exit;

final class OMS_Woo_Sync {
    private static bool $applying = false;
    public static function init(): void {
        add_action('action_scheduler_init', static function () {
            if (!as_has_scheduled_action('oms_woo_tick', array(), 'oms-woo')) {
                as_schedule_recurring_action(time() + 60, 600, 'oms_woo_tick', array(), 'oms-woo', true);
            }
        });
        foreach (array('woocommerce_new_order', 'woocommerce_update_order', 'woocommerce_checkout_order_processed', 'woocommerce_store_api_checkout_order_processed') as $hook) {
            add_action($hook, array(__CLASS__, 'queue'), 20, 1);
        }
        add_action('oms_woo_send', array(__CLASS__, 'send'), 10, 2);
        add_action('oms_woo_scan', array(__CLASS__, 'scan'));
        add_action('oms_woo_tick', array(__CLASS__, 'tick'));
    }
    public static function queue($order, int $attempt = 0): void {
        $id = $order instanceof WC_Order ? $order->get_id() : absint($order);
        if (!$id || self::$applying || !OMS_Woo_API::connected() || !function_exists('as_schedule_single_action')) { return; }
        as_schedule_single_action(time() + ($attempt ? min(3600, 30 * (2 ** min($attempt, 7))) : 10),
            'oms_woo_send', array($id, $attempt), 'oms-woo', true);
    }
    private static function address(WC_Order $o, string $type): array {
        $a = $o->get_address($type);
        return array('firstName' => $a['first_name'] ?? '', 'lastName' => $a['last_name'] ?? '',
            'company' => $a['company'] ?? '', 'address1' => $a['address_1'] ?? '', 'address2' => $a['address_2'] ?? '',
            'city' => $a['city'] ?? '', 'state' => $a['state'] ?? '', 'postcode' => $a['postcode'] ?? '',
            'country' => $a['country'] ?? '', 'email' => $a['email'] ?? '', 'phone' => $a['phone'] ?? '');
    }
    public static function payload(WC_Order $o): array {
        $items = array();
        foreach ($o->get_items('line_item') as $id => $item) {
            $product = $item->get_product();
            $image_ids = array();
            if ($product) {
                $image_ids = array_merge(array($product->get_image_id()), $product->get_gallery_image_ids());
                if ($product->is_type('variation')) {
                    $parent = wc_get_product($product->get_parent_id());
                    if ($parent) $image_ids = array_merge($image_ids, array($parent->get_image_id()), $parent->get_gallery_image_ids());
                }
            }
            $image_urls = array();
            foreach (array_unique(array_filter($image_ids)) as $image_id) {
                $url = wp_get_attachment_image_url($image_id, 'large');
                if ($url && !in_array($url, $image_urls, true)) $image_urls[] = $url;
            }
            $attributes = array();
            foreach ($item->get_formatted_meta_data('') as $meta) {
                $attributes[] = wp_strip_all_tags($meta->display_key . ': ' . $meta->display_value);
            }
            $items[] = array('id' => (int) $id, 'productId' => $item->get_product_id(), 'variationId' => $item->get_variation_id(),
                'name' => $item->get_name(), 'sku' => $product ? $product->get_sku() : '', 'quantity' => $item->get_quantity(),
                'total' => (float) $item->get_total(), 'attributes' => implode('; ', $attributes),
                'imageUrl' => $image_urls[0] ?? '', 'imageUrls' => $image_urls);
        }
        $modified = $o->get_date_modified() ?: $o->get_date_created();
        return array('id' => $o->get_id(), 'number' => (string) $o->get_order_number(), 'customerId' => $o->get_customer_id(),
            'status' => $o->get_status(), 'currency' => $o->get_currency(), 'total' => (float) $o->get_total(),
            'paymentMethod' => $o->get_payment_method(), 'customerNote' => $o->get_customer_note(),
            'modifiedAt' => gmdate('Y-m-d\TH:i:s\Z', $modified ? $modified->getTimestamp() : time()),
            'billing' => self::address($o, 'billing'), 'shipping' => self::address($o, 'shipping'), 'items' => $items);
    }
    public static function send(int $id, int $attempt = 0): void {
        if (!OMS_Woo_API::connected()) { return; }
        $order = wc_get_order($id);
        if (!$order || $order->get_type() !== 'shop_order' || $order->get_status() === 'checkout-draft') { return; }
        $payload = self::payload($order);
        if (!$payload['items']) { return; }
        $content = $payload;
        unset($content['modifiedAt']); // Saving connector metadata must not cause an endless re-sync loop.
        $hash = hash('sha256', wp_json_encode($content));
        $c = OMS_Woo_API::config();
        $marker = hash('sha256', rtrim($c['api_url'], '/')) . ':' . (string) $c['connection_id'] . ':' . $hash;
        if ($order->get_meta('_oms_synced_hash') === $marker) { return; }
        $result = OMS_Woo_API::request('orders', $payload);
        if (is_wp_error($result)) {
            OMS_Woo_API::log($id, 'failed', $result->get_error_message());
            self::$applying = true;
            try { $order->update_meta_data('_oms_sync_failed', true); $order->save(); }
            finally { self::$applying = false; }
            // Durable retries continue hourly after backoff reaches its cap, including long API outages.
            self::queue($id, $attempt + 1);
            return;
        }
        self::$applying = true;
        try {
            $order->update_meta_data('_oms_synced_hash', $marker);
            $order->update_meta_data('_oms_order_id', (int) ($result['orderId'] ?? 0));
            $order->delete_meta_data('_oms_sync_failed');
            $order->save();
        } finally { self::$applying = false; }
        update_option('oms_woo_last_sync', gmdate('c'), false);
        OMS_Woo_API::log($id, 'synced', $result['result'] ?? 'Order accepted.');
    }
    public static function start_scan(int $from = 0, bool $reconcile = false): bool {
        if (get_option('oms_woo_scan_state')) { return false; }
        update_option('oms_woo_scan_state', array('from' => $from, 'until' => time(), 'page' => 1, 'queued' => 0, 'reconcile' => $reconcile), false);
        as_schedule_single_action(time() + 1, 'oms_woo_scan', array(), 'oms-woo');
        return true;
    }
    public static function scan(): void {
        if (!OMS_Woo_API::connected()) { return; }
        $state = get_option('oms_woo_scan_state');
        if (!$state) { return; }
        $args = array('type' => 'shop_order', 'limit' => 50, 'page' => (int) $state['page'],
            'orderby' => 'ID', 'order' => 'ASC', 'return' => 'ids',
            (!empty($state['reconcile']) ? 'date_modified' : 'date_created') => (int) $state['from'] . '...' . (int) $state['until']);
        foreach (($ids = wc_get_orders($args)) as $id) { self::queue($id); }
        $state['queued'] += count($ids);
        if (count($ids) === 50) {
            $state['page']++;
            update_option('oms_woo_scan_state', $state, false);
            as_schedule_single_action(time() + 15, 'oms_woo_scan', array(), 'oms-woo');
        } else {
            if (!empty($state['reconcile'])) {
                update_option('oms_woo_reconcile_from', max(0, (int) $state['until'] - 120), false);
            }
            update_option('oms_woo_last_import', $state['queued'] . ' orders scanned at ' . gmdate('c'), false);
            delete_option('oms_woo_scan_state');
            OMS_Woo_API::log(0, 'scan-complete', $state['queued'] . ' orders scanned; changed orders queued.');
        }
    }
    public static function tick(): void {
        if (!OMS_Woo_API::connected()) { return; }
        if (get_option('oms_woo_scan_state') && !as_has_scheduled_action('oms_woo_scan', array(), 'oms-woo')) {
            as_schedule_single_action(time() + 1, 'oms_woo_scan', array(), 'oms-woo');
        }
        $cursor = (int) get_option('oms_woo_updates_cursor', 0);
        $result = OMS_Woo_API::request('updates?after=' . $cursor);
        if (is_wp_error($result)) { OMS_Woo_API::log(0, 'poll-failed', $result->get_error_message()); return; }
        $request_id = $result['syncRequestId'] ?? '';
        if ($request_id && $request_id !== get_option('oms_woo_sync_request')) {
            if (self::start_scan(0)) { update_option('oms_woo_sync_request', $request_id, false); }
        }
        foreach (($result['items'] ?? array()) as $item) {
            $order = wc_get_order((int) $item['externalOrderId']);
            if (!$order || !empty($item['deleted']) || $order->get_meta('_oms_revision') === $item['revision']) { continue; }
            self::$applying = true;
            try {
                $status = $item['status'] ?? null;
                if ($status && isset(wc_get_order_statuses()['wc-' . $status]) && $order->get_status() !== $status) {
                    // Woo status changes can trigger store emails and other installed extensions.
                    $order->set_status($status, 'Status synchronized from OMS.');
                }
                $order->update_meta_data('_oms_tracking_number', sanitize_text_field($item['trackingNumber'] ?? ''));
                $order->update_meta_data('_oms_revision', $item['revision']);
                $order->save();
            } finally { self::$applying = false; }
            self::queue($order->get_id());
        }
        update_option('oms_woo_updates_cursor', (int) ($result['nextCursor'] ?? 0), false);
        // Recover missed events without silently importing older history outside the chosen range.
        if (!get_option('oms_woo_scan_state')) { self::start_scan((int) get_option('oms_woo_reconcile_from', time() - 600), true); }
    }
}
