import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoDirective } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { Product } from 'src/app/business/entities/entities.generated';
import { Column, SpiderlyDataTableComponent } from 'spiderly';

// Overrides the spiderly-cli-generated product-list (which ships with only an Id
// column) so e2e tests can exercise text, numeric, and boolean filters + multi-sort
// across varied column types. Column labels are literal English for stable
// Playwright text matching; production apps typically use translocoService.translate.
@Component({
    selector: 'product-list',
    templateUrl: './product-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent,
    ]
})
export class ProductListComponent implements OnInit {
    cols: Column<Product>[];

    getPaginatedProductListObservableMethod = this.apiService.getPaginatedProductList;
    exportProductListToExcelObservableMethod = this.apiService.exportProductListToExcel;
    deleteProductObservableMethod = this.apiService.deleteProduct;
    deleteProductListObservableMethod = this.apiService.deleteProductList;

    constructor(
        private apiService: ApiService,
    ) { }

    ngOnInit() {
        this.cols = [
            { name: 'Id', filterType: 'numeric', field: 'id' },
            { name: 'Name', filterType: 'text', field: 'name' },
            { name: 'Price', filterType: 'numeric', field: 'price' },
            { name: 'Stock', filterType: 'numeric', field: 'stock' },
            { name: 'IsActive', filterType: 'boolean', field: 'isActive' },
            { actions: [
                { name: 'Details', field: 'Details' },
                { name: 'Delete', field: 'Delete' },
            ]},
        ];
    }
}
