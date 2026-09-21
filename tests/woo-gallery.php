<?php
// Isolated payload regression test; does not load WordPress or touch any database.
define('ABSPATH', __DIR__);
function wp_get_attachment_image_url($id, $size) { return $id ? 'https://store.example.test/' . $id . '.jpg' : false; }
function wc_get_product($id) { return new GalleryProduct(false); }
class GalleryProduct {
    public function __construct(private bool $variation) {}
    public function get_image_id() { return $this->variation ? 3 : 1; }
    public function get_gallery_image_ids() { return $this->variation ? array() : array(1,2,3); }
    public function is_type($type) { return $this->variation && $type === 'variation'; }
    public function get_parent_id() { return 99; }
    public function get_sku() { return 'TEST'; }
}
class GalleryItem {
    public function __construct(private bool $variation) {}
    public function get_product() { return new GalleryProduct($this->variation); }
    public function get_formatted_meta_data($v) { return array(); }
    public function get_product_id() { return 99; }
    public function get_variation_id() { return $this->variation ? 100 : 0; }
    public function get_name() { return 'Test product'; }
    public function get_quantity() { return 2; }
    public function get_total() { return '20'; }
}
class WC_Order {
    public function __construct(private bool $variation) {}
    public function get_items($type) { return array(1=>new GalleryItem($this->variation)); }
    public function get_address($type) { return array(); }
    public function get_date_modified() { return null; }
    public function get_date_created() { return null; }
    public function get_id() { return 1; }
    public function get_order_number() { return '1'; }
    public function get_customer_id() { return 0; }
    public function get_status() { return 'processing'; }
    public function get_currency() { return 'USD'; }
    public function get_total() { return '20'; }
    public function get_payment_method() { return 'cod'; }
    public function get_customer_note() { return ''; }
}
require __DIR__ . '/../WooCommerce/oms-woocommerce/includes/class-oms-sync.php';
foreach (array(false,true) as $variation) {
    $line=OMS_Woo_Sync::payload(new WC_Order($variation))['items'][0];
    $expected=$variation ? array(3,1,2) : array(1,2,3);
    $urls=array_map(fn($id)=>'https://store.example.test/'.$id.'.jpg',$expected);
    if ($line['imageUrls'] !== $urls || $line['imageUrl'] !== $urls[0]) throw new RuntimeException('Gallery regression');
    echo $variation ? "PASS variation image + parent gallery, deduplicated\n" : "PASS featured + simple product gallery, deduplicated\n";
}
