import React, { Component } from 'react';
import { Route } from 'react-router';
import { Layout } from './navigation/Layout';
import DashBoard  from './components/dashboard/Dashboard.js';
import ProjectCreator from './components/projectcreation/ProjectCreator.js'
import AdminPanel from './components/admin/AdminPanel.js';
import ProjectView from './components/projectview/ProjectView.js'
import ProjectStimuli from './components/projectStimuli/ProjectStimuli.js';


export class Main extends Component {
    static displayName = Main.name;
    constructor(props) {
        super(props);
    }
    
    componentDidMount() {
        if(this.props.location.state === undefined){
            this.props.history.push({
                pathname: '/'
            });
        } else {
            this.setState({
                userData: this.props.location.state.userData
            });
        }
    }

    render() {
        return (
            <div>
                {this.state ? (
                    <Layout props={this.state.userData}>
                        <Route path='/home' component={DashBoard}/>
                        <Route path='/create-project' component={ProjectCreator}/>
                        <Route path='/admin-panel' component={AdminPanel}/>
                        <Route path='/project-view' component={ProjectView}/>
                        <Route path='/project-stimuli' component={ProjectStimuli}/>
                    </Layout>
                ): (null)}
            </div>
            );
    }
}