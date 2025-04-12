import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PanelBodyComponent } from './panel-body/panel-body.component';
import { PanelFooterComponent } from './panel-footer/panel-footer.component';
import { PanelHeaderComponent } from './panel-header/panel-header.component';
import { SpiderlyPanelComponent } from './spiderly-panel/spiderly-panel.component';
import { SpiderlyCardComponent } from './spiderly-card/spiderly-card.component';
import { PrimengModule } from '../../modules/primeng.module';

@NgModule({
  imports: [
    CommonModule,
    PrimengModule,
  ],
  exports: [
    PanelHeaderComponent,
    PanelBodyComponent,
    PanelFooterComponent,
    SpiderlyPanelComponent,
    SpiderlyCardComponent
  ],
  declarations: [
    PanelHeaderComponent,
    PanelBodyComponent,
    PanelFooterComponent,
    SpiderlyPanelComponent,
    SpiderlyCardComponent
  ],
  providers: [
  ]
})
export class SpiderlyPanelsModule {}